using System.Numerics;

namespace ProjectZ.AudioKeys;

/// <summary>Measures one or two PCM channels into reusable frequency bands and envelope state.</summary>
public sealed class AudioKeyAnalyzer
{
    private readonly float[] _weights;
    private readonly float[] _real;
    private readonly float[] _imaginary;
    private readonly float[] _cosine;
    private readonly float[] _sine;
    private readonly int[] _order;
    private readonly float[] _power;
    private readonly float[] _peaks;
    private readonly BandEnvelope[] _bands;
    private readonly FrequencySpan[] _spectrum;
    private readonly double[] _centers;
    private readonly float _windowSum;
    private readonly float _powerScale;
    private readonly bool _needsPeaks;

    public AudioKeyAnalyzer(int sampleRate, int windowSize, IReadOnlyList<AudioKeyBand> bands, int spectrumSize = 96)
    {
        if (sampleRate is < 8_000 or > 384_000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (windowSize is < 256 or > 8192 || !BitOperations.IsPow2(windowSize))
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count is < 1 or > 16 || spectrumSize is < 0 or > 256)
            throw new ArgumentOutOfRangeException(nameof(bands));

        _weights = new float[windowSize];
        _real = new float[windowSize];
        _imaginary = new float[windowSize];
        _cosine = new float[windowSize / 2];
        _sine = new float[windowSize / 2];
        _order = new int[windowSize];
        _power = new float[windowSize / 2 + 1];
        _peaks = new float[_power.Length];
        var energy = 0f;
        for (var index = 0; index < windowSize; index++)
        {
            var weight = (1f - MathF.Cos(index * MathF.Tau / windowSize)) * .5f;
            _weights[index] = weight;
            _windowSum += weight;
            energy += weight * weight;
            _order[index] = (_order[index >> 1] >> 1) | ((index & 1) * (windowSize / 2));
        }
        for (var index = 0; index < _cosine.Length; index++)
            (_sine[index], _cosine[index]) = MathF.SinCos(-index * MathF.Tau / windowSize);
        _powerScale = 1f / (windowSize * energy);

        var binWidth = sampleRate / (float)windowSize;
        _bands = bands.Select(band =>
        {
            ArgumentNullException.ThrowIfNull(band);
            return new BandEnvelope(band.Validate(sampleRate), new FrequencySpan(band.LowHz, band.HighHz, binWidth, windowSize / 2));
        }).ToArray();
        _needsPeaks = _bands.Any(band => band.Mode == AudioKeyMode.Peak);
        _spectrum = new FrequencySpan[spectrumSize];
        _centers = new double[spectrumSize];
        var edge = 20d;
        var ratio = Math.Pow(sampleRate / 40d, 1d / Math.Max(1, spectrumSize));
        for (var index = 0; index < spectrumSize; index++)
        {
            var high = index + 1 == spectrumSize ? sampleRate * .5 : edge * ratio;
            _spectrum[index] = new FrequencySpan(edge, high, binWidth, windowSize / 2);
            _centers[index] = Math.Sqrt(edge * high);
            edge = high;
        }
    }

    public ReadOnlySpan<double> SpectrumFrequencies => _centers;
    public int SpectrumPowerBins => _power.Length;

    /// <summary>Accumulates linear bin power so an offline caller can project the averaged spectrum once.</summary>
    public void AddSpectrumPowerTo(Span<double> sum)
    {
        if (sum.Length != _power.Length)
            throw new ArgumentException("Supply one accumulator for each spectrum bin.", nameof(sum));
        for (var bin = 1; bin < _power.Length; bin++)
            sum[bin] += _power[bin];
    }

    public void WriteMeanSpectrum(ReadOnlySpan<double> sum, int frames, Span<double> spectrum)
    {
        if (sum.Length != _power.Length || spectrum.Length != _spectrum.Length || frames < 0)
            throw new ArgumentException("The accumulator and spectrum must match the analyzer dimensions.");
        if (frames == 0)
        {
            spectrum.Clear();
            return;
        }
        for (var index = 0; index < spectrum.Length; index++)
            spectrum[index] = Math.Min(1, Math.Sqrt(_spectrum[index].Sum(sum) / frames));
    }

    public void Analyze(ReadOnlySpan<float> left, ReadOnlySpan<float> right, double seconds,
        Span<float> levels, Span<float> spectrum = default)
    {
        if (left.Length != _real.Length || !right.IsEmpty && right.Length != left.Length)
            throw new ArgumentException("Supply the configured number of samples per channel.", nameof(left));
        if (levels.Length < _bands.Length || !spectrum.IsEmpty && spectrum.Length != _spectrum.Length)
            throw new ArgumentException("Provide a destination for each configured band and spectrum value.");
        var stereo = !right.IsEmpty && !left.SequenceEqual(right);
        Transform(left, add: false, weight: stereo ? .5f : 1f);
        if (stereo)
            Transform(right, add: true, weight: .5f);

        var elapsed = CheckedDuration(seconds);
        for (var index = 0; index < _bands.Length; index++)
        {
            var band = _bands[index];
            var magnitude = band.Mode == AudioKeyMode.Peak
                ? MathF.Sqrt(band.Range.Maximum(_peaks))
                : MathF.Sqrt(band.Range.Sum(_power));
            levels[index] = band.Advance(magnitude, elapsed);
        }
        for (var index = 0; index < spectrum.Length; index++)
            spectrum[index] = Math.Min(1, MathF.Sqrt(_spectrum[index].Sum(_power)));
    }

    public void Decay(double seconds, Span<float> levels)
    {
        if (levels.Length < _bands.Length)
            throw new ArgumentException("Provide a destination for each frequency band.", nameof(levels));
        var elapsed = CheckedDuration(seconds);
        for (var index = 0; index < _bands.Length; index++)
            levels[index] = _bands[index].Advance(0, elapsed);
    }

    private static float CheckedDuration(double seconds) =>
        double.IsFinite(seconds) && seconds >= 0
            ? (float)Math.Min(seconds, 10)
            : throw new ArgumentOutOfRangeException(nameof(seconds));

    private void Transform(ReadOnlySpan<float> samples, bool add, float weight)
    {
        var dc = 0f;
        for (var index = 0; index < samples.Length; index++)
        {
            var weighted = (float.IsFinite(samples[index]) ? samples[index] : 0) * _weights[index];
            _real[_order[index]] = weighted;
            dc += weighted;
        }
        Array.Clear(_imaginary);

        for (var span = 2; span <= _real.Length; span <<= 1)
        {
            var half = span / 2;
            var step = _real.Length / span;
            for (var start = 0; start < _real.Length; start += span)
            for (var offset = 0; offset < half; offset++)
            {
                var a = start + offset;
                var b = a + half;
                var cos = _cosine[offset * step];
                var sin = _sine[offset * step];
                var re = _real[b] * cos - _imaginary[b] * sin;
                var im = _real[b] * sin + _imaginary[b] * cos;
                _real[b] = _real[a] - re;
                _imaginary[b] = _imaginary[a] - im;
                _real[a] += re;
                _imaginary[a] += im;
            }
        }

        var amplitude = 2f / _windowSum;
        for (var bin = 1; bin < _power.Length; bin++)
        {
            // A constant multiplied by the periodic Hann window leaks only into bin 1.
            var real = _real[bin] + (bin == 1 ? dc * .5f : 0);
            var imaginary = _imaginary[bin];
            var squared = (real * real + imaginary * imaginary) * weight;
            var factor = bin + 1 == _power.Length ? 1f : 2f;
            var energy = squared * _powerScale * factor;
            _power[bin] = add ? _power[bin] + energy : energy;
            if (_needsPeaks)
            {
                var peakSquared = squared * amplitude * amplitude * (.25f * factor * factor);
                _peaks[bin] = add ? _peaks[bin] + peakSquared : peakSquared;
            }
        }
    }

    private sealed class BandEnvelope(AudioKeyBand options, FrequencySpan range)
    {
        private float _value;
        private float _baseline;
        private float _fast;
        private float _cooldown;
        private bool _above;

        public AudioKeyMode Mode => options.Mode;
        public FrequencySpan Range => range;

        public float Advance(float level, float seconds)
        {
            var threshold = (float)options.Threshold;
            var target = Math.Max(0, level - threshold);
            if (Mode == AudioKeyMode.Trigger)
            {
                var above = level > threshold;
                target = above && !_above ? 1 : 0;
                _above = above;
            }
            else if (Mode == AudioKeyMode.Onset)
            {
                _cooldown = Math.Max(0, _cooldown - seconds);
                var increase = level - _fast;
                var novelty = Math.Max(.018f, _baseline * .12f);
                target = 0;
                if (_cooldown == 0 && level > threshold && increase > novelty)
                {
                    target = Math.Clamp(.22f + increase * 2.8f, 0, 1);
                    _cooldown = .085f;
                }
                _fast = Follow(_fast, level, seconds, .027);
                _baseline = Follow(_baseline, level, seconds, .24);
            }
            target = Math.Min(1, target * (float)options.Gain);
            var attack = Mode is AudioKeyMode.Trigger or AudioKeyMode.Onset ? 0 : options.AttackSeconds;
            _value = Follow(_value, target, seconds, target > _value ? attack : options.ReleaseSeconds);
            return _value;
        }

        private static float Follow(float value, float target, float seconds, double time) =>
            time == 0 ? target : target + (value - target) * MathF.Exp(-seconds / (float)time);
    }

    private readonly struct FrequencySpan
    {
        private readonly int _first;
        private readonly float[] _weights;

        public FrequencySpan(double low, double high, float binWidth, int end)
        {
            _first = Math.Max(1, (int)(low / binWidth + .5));
            var last = Math.Min(end, (int)Math.Ceiling(high / binWidth + .5) - 1);
            _weights = new float[Math.Max(0, last + 1 - _first)];
            for (var index = 0; index < _weights.Length; index++)
            {
                var bin = _first + index;
                var startHz = (bin - .5) * binWidth;
                var endHz = Math.Min(end * binWidth, (bin + .5) * binWidth);
                _weights[index] = (float)(Math.Max(0, Math.Min(high, endHz) - Math.Max(low, startHz)) / (endHz - startHz));
            }
        }

        public float Sum(ReadOnlySpan<float> magnitudes)
        {
            var output = 0f;
            for (var index = 0; index < _weights.Length; index++)
                output += magnitudes[_first + index] * _weights[index];
            return output;
        }

        public double Sum(ReadOnlySpan<double> magnitudes)
        {
            var output = 0d;
            for (var index = 0; index < _weights.Length; index++)
                output += magnitudes[_first + index] * _weights[index];
            return output;
        }

        public float Maximum(ReadOnlySpan<float> magnitudes)
        {
            var output = 0f;
            for (var index = 0; index < _weights.Length; index++)
                output = Math.Max(output, magnitudes[_first + index] * _weights[index] * _weights[index]);
            return output;
        }
    }
}
