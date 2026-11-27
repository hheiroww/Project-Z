using System.Numerics;

namespace ProjectZ.StudioScenes;

/// <summary>A sample of an evolving ocean at a point on its undisplaced horizontal plane.</summary>
public readonly record struct StudioOceanSample(Vector3 Displacement, Vector2 Slope, float Jacobian, float Foam)
{
    public float Height => Displacement.Y;
    public Vector3 Normal => Vector3.Normalize(new Vector3(-Slope.X, 1f, -Slope.Y));
}

/// <summary>
/// Builds continuous, repeatable ocean fields in world metres. Update once per presentation frame,
/// then sample from any number of render workers before the next update.
/// </summary>
/// <remarks>
/// Adapted from Garrett Gunnell's MIT-licensed Water FFT pipeline
/// (https://github.com/GarrettGunnell/Water/tree/1673a12e796c5745aea6fa26eda53261da8efa80).
/// The upstream spectrum math also references Ivan Pensionerov's MIT-licensed FFT-Ocean.
/// This portable CPU version uses radix-2 transforms and four independent cascade lengths.
/// Retain LICENSE.Water.txt when redistributing it.
/// </remarks>
public sealed class SpectralOcean
{
    private const float RepeatSeconds = 200;
    private const int PreviewResolution = 32;
    private const int CaptureResolution = 64;
    private static readonly float[] Lengths = [192, 63, 19, 5.5f];
    private readonly record struct SpectrumKey(int Seed, float WindSpeed, float Direction, float Fetch, float Depth, float Swell, int Resolution);

    private SpectrumKey _key;
    private Cascade[] _cascades = [];
    private double _lastTime = double.NaN;
    private float _waveScale;
    private float _chopScale;
    private float _foam;

    public void Update(StudioOcean settings, double seconds, StudioSignal signal = default, bool highQuality = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        if (!double.IsFinite(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));

        var size = highQuality ? CaptureResolution : PreviewResolution;
        var key = new SpectrumKey(settings.Seed, settings.WindSpeed, settings.WindDirection, settings.Fetch,
            settings.Depth, settings.Swell, size);
        if (_cascades.Length == 0 || _key != key)
        {
            _cascades = new Cascade[Lengths.Length];
            for (var index = 0; index < Lengths.Length; index++)
            {
                var lower = index == 0 ? 0f : Boundary(Lengths[index - 1]);
                var upper = index == Lengths.Length - 1 ? float.PositiveInfinity : Boundary(Lengths[index]);
                _cascades[index] = new Cascade(size, Lengths[index], lower, upper, index, key);
            }
            _key = key;
            _lastTime = double.NaN;
        }

        var response = .48f * Unit(signal.Bass) + .52f * Unit(signal.Beat);
        _waveScale = settings.WaveScale * (1f + settings.AudioResponse * response);
        var chop = settings.Choppiness * _waveScale;
        _foam = settings.Foam;
        if (_lastTime == seconds && _chopScale == chop)
            return;

        var elapsed = seconds - _lastTime;
        var continuity = double.IsFinite(elapsed) && elapsed >= 0 && elapsed < .42;
        var decay = continuity ? MathF.Exp(-1.65f * (float)elapsed) : 0f;
        var phase = (float)Math.IEEERemainder(seconds, RepeatSeconds);
        foreach (var cascade in _cascades)
            cascade.Evolve(phase, chop, decay);
        _lastTime = seconds;
        _chopScale = chop;
    }

    public float SampleHeight(float x, float z, float footprint = 0)
    {
        if (!float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(footprint))
            return 0;

        var height = 0f;
        foreach (var cascade in _cascades)
        {
            var weight = cascade.Filter(footprint);
            if (weight > 0)
                height += cascade.HeightAt(x, z) * weight;
        }
        return height * _waveScale;
    }

    public StudioOceanSample Sample(float x, float z, float footprint = 0)
    {
        if (!float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(footprint))
            return new StudioOceanSample(default, default, 1, 0);

        var real = Vector4.Zero;
        var imaginary = Vector4.Zero;
        var clearWater = 1f;
        foreach (var cascade in _cascades)
        {
            var weight = cascade.Filter(footprint);
            if (weight <= 0)
                continue;
            cascade.Sample(x, z, out var a, out var b, out var foam);
            real += a * weight;
            imaginary += b * weight;
            clearWater *= 1f - foam * weight;
        }

        var displacement = new Vector3(real.Y * _chopScale, real.X * _waveScale, imaginary.Y * _chopScale);
        var dxx = real.W * _chopScale;
        var dzz = imaginary.W * _chopScale;
        var dxz = imaginary.X * _chopScale;
        var jacobian = (1f + dxx) * (1f + dzz) - dxz * dxz;
        var slopes = new Vector2(real.Z / (1f + MathF.Abs(dxx)), imaginary.Z / (1f + MathF.Abs(dzz))) * _waveScale;
        var foamCoverage = Math.Max(1f - clearWater, Breaking(jacobian));
        return new StudioOceanSample(displacement, slopes, jacobian, Unit(foamCoverage * _foam));
    }

    private static float Boundary(float length) => .70f * MathF.PI * PreviewResolution / length;
    private static float Unit(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
    private static float Breaking(float jacobian) => 1f - SmoothStep(.18f, .86f, jacobian);

    private static float SmoothStep(float lower, float upper, float value)
    {
        var t = Math.Clamp((value - lower) / (upper - lower), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private sealed class Cascade
    {
        private const float Gravity = 9.81f;
        private const float TwoPi = 2 * MathF.PI;
        private readonly int _size;
        private readonly int _mask;
        private readonly float _worldScale;
        private readonly float _cellSpan;
        private readonly Mode[] _modes;
        private readonly Vector4[] _real;
        private readonly Vector4[] _imaginary;
        private readonly float[] _foam;
        private readonly int[] _reverse;
        private readonly Vector2[] _twiddle;

        private readonly record struct Mode(Vector2 H0, float Omega, float Kx, float Kz, float InverseLength);
        private readonly record struct Address(int A, int B, int C, int D, float X, float Z);

        internal Cascade(int size, float length, float lower, float upper, int index, SpectrumKey settings)
        {
            _size = size;
            _mask = size - 1;
            _worldScale = 1f / length;
            _cellSpan = length / size;
            _modes = new Mode[size * size];
            _real = new Vector4[_modes.Length];
            _imaginary = new Vector4[_modes.Length];
            _foam = new float[_modes.Length];
            _reverse = new int[size];
            _twiddle = new Vector2[size / 2];
            for (var i = 1; i < size; i++)
                _reverse[i] = (_reverse[i >> 1] >> 1) | ((i & 1) * (size / 2));
            for (var i = 0; i < _twiddle.Length; i++)
            {
                var (sine, cosine) = MathF.SinCos(TwoPi * i / size);
                _twiddle[i] = new Vector2(cosine, sine);
            }

            var spacing = TwoPi / length;
            var wind = settings.Direction * (MathF.PI / 180f);
            var speed = settings.WindSpeed;
            var fetch = settings.Fetch;
            var alpha = .076f * MathF.Pow(Gravity * fetch / (speed * speed), -.22f);
            var peak = 22f * MathF.Pow(speed * fetch / (Gravity * Gravity), -.33f);
            var depth = settings.Depth;
            var quantum = TwoPi / RepeatSeconds;
            for (var row = 0; row < size; row++)
            for (var column = 0; column < size; column++)
            {
                // Nyquist lines are their own mirrored partners along one axis; dropping
                // them keeps the odd displacement derivatives Hermitian.
                if (column == size / 2 || row == size / 2 || (column == 0 && row == 0))
                    continue;
                var waveX = column < size / 2 ? column : column - size;
                var waveZ = row < size / 2 ? row : row - size;
                var kx = waveX * spacing;
                var kz = waveZ * spacing;
                var radial = MathF.Sqrt(kx * kx + kz * kz);
                var band = (lower <= 0f ? 1f : SmoothStep(lower * .78f, lower * 1.22f, radial))
                    * (float.IsFinite(upper) ? 1f - SmoothStep(upper * .78f, upper * 1.22f, radial) : 1f);
                if (band <= 0f)
                    continue;

                var kd = Math.Min(radial * depth, 20f);
                var depthFactor = MathF.Tanh(kd);
                var omega = MathF.Sqrt(Gravity * radial * depthFactor);
                var derivative = Gravity * (depthFactor + kd * (1f - depthFactor * depthFactor)) / (2f * omega);
                var angle = MathF.Atan2(kz, kx);
                var main = Jonswap(omega, peak, alpha, 3.3f, depth) * Directional(angle, wind, omega / peak, settings.Swell);
                var swellPeak = peak * .77f;
                var secondary = Jonswap(omega, swellPeak, alpha * .70f, 5f, depth)
                    * Directional(angle, wind - .85f, omega / swellPeak, settings.Swell);
                var swellWeight = .34f * settings.Swell;
                var shortWave = radial * .024f;
                var density = band * (main * (1f - swellWeight) + secondary * swellWeight) * MathF.Exp(-shortWave * shortWave);
                var amplitude = MathF.Sqrt(2f * density * derivative / radial * spacing * spacing);
                var gaussian = Gaussian(settings.Seed, index, waveX, waveZ);
                _modes[row * size + column] = new Mode(gaussian * amplitude, MathF.Floor(omega / quantum) * quantum,
                    kx, kz, 1f / radial);
            }
        }

        internal float Filter(float footprint) => 1f - SmoothStep(.8f * _cellSpan, 2.3f * _cellSpan, footprint);

        internal void Evolve(float time, float chopScale, float foamDecay)
        {
            for (var row = 0; row < _size; row++)
            for (var column = 0; column < _size; column++)
            {
                var index = row * _size + column;
                var mode = _modes[index];
                var mirrored = _modes[((-row & _mask) * _size) + (-column & _mask)].H0;
                var (sine, cosine) = MathF.SinCos(mode.Omega * time);
                // h(k,t) = h0(k)e^(iwt) + conjugate(h0(-k))e^(-iwt).
                var height = new Vector2((mode.H0.X + mirrored.X) * cosine - (mode.H0.Y + mirrored.Y) * sine,
                    (mode.H0.X - mirrored.X) * sine + (mode.H0.Y - mirrored.Y) * cosine);
                var ih = new Vector2(-height.Y, height.X);
                var displacementX = ih * (mode.Kx * mode.InverseLength);
                var displacementZ = ih * (mode.Kz * mode.InverseLength);
                var slopeX = ih * mode.Kx;
                var slopeZ = ih * mode.Kz;
                var inverseHeight = -height * mode.InverseLength;
                var derivativeX = inverseHeight * (mode.Kx * mode.Kx);
                var derivativeZ = inverseHeight * (mode.Kz * mode.Kz);
                var derivativeCross = inverseHeight * (mode.Kx * mode.Kz);

                // Each complex channel carries two independent real-valued fields.
                _real[index] = new Vector4(height.X - derivativeCross.Y, displacementX.X - displacementZ.Y,
                    slopeX.X - slopeZ.Y, derivativeX.X - derivativeZ.Y);
                _imaginary[index] = new Vector4(height.Y + derivativeCross.X, displacementX.Y + displacementZ.X,
                    slopeX.Y + slopeZ.X, derivativeX.Y + derivativeZ.X);
            }
            for (var i = 0; i < _size; i++)
                Inverse(i * _size, 1);
            for (var i = 0; i < _size; i++)
                Inverse(i, _size);

            for (var i = 0; i < _foam.Length; i++)
            {
                var cross = _imaginary[i].X * chopScale;
                var determinant = (1f + _real[i].W * chopScale) * (1f + _imaginary[i].W * chopScale) - cross * cross;
                _foam[i] = Math.Max(_foam[i] * foamDecay, Breaking(determinant));
            }
        }

        private void Inverse(int start, int stride)
        {
            for (var index = 1; index < _size; index++)
            {
                var reversed = _reverse[index];
                if (index >= reversed)
                    continue;
                var left = start + index * stride;
                var right = start + reversed * stride;
                (_real[left], _real[right]) = (_real[right], _real[left]);
                (_imaginary[left], _imaginary[right]) = (_imaginary[right], _imaginary[left]);
            }
            for (var period = 2; period <= _size; period <<= 1)
            {
                var half = period / 2;
                var step = _size / period;
                for (var group = 0; group < _size; group += period)
                for (var element = 0; element < half; element++)
                {
                    var a = start + (group + element) * stride;
                    var b = a + half * stride;
                    var rotation = _twiddle[element * step];
                    var real = _real[b] * rotation.X - _imaginary[b] * rotation.Y;
                    var imaginary = _real[b] * rotation.Y + _imaginary[b] * rotation.X;
                    _real[b] = _real[a] - real;
                    _imaginary[b] = _imaginary[a] - imaginary;
                    _real[a] += real;
                    _imaginary[a] += imaginary;
                }
            }
            // As in the reference compute pass, leave the inverse transform unnormalized.
            // Mode amplitudes already include the square root of the wave-vector cell area.
        }

        internal float HeightAt(float x, float z)
        {
            var address = Coordinates(x, z);
            return Bilinear(_real[address.A].X, _real[address.B].X, _real[address.C].X, _real[address.D].X, address.X, address.Z);
        }

        internal void Sample(float x, float z, out Vector4 real, out Vector4 imaginary, out float foam)
        {
            var a = Coordinates(x, z);
            real = Vector4.Lerp(Vector4.Lerp(_real[a.A], _real[a.B], a.X), Vector4.Lerp(_real[a.C], _real[a.D], a.X), a.Z);
            imaginary = Vector4.Lerp(Vector4.Lerp(_imaginary[a.A], _imaginary[a.B], a.X),
                Vector4.Lerp(_imaginary[a.C], _imaginary[a.D], a.X), a.Z);
            foam = Bilinear(_foam[a.A], _foam[a.B], _foam[a.C], _foam[a.D], a.X, a.Z);
        }

        private Address Coordinates(float x, float z)
        {
            var u = x * _worldScale;
            var v = z * _worldScale;
            u = (u - MathF.Floor(u)) * _size;
            v = (v - MathF.Floor(v)) * _size;
            var i = (int)u;
            var j = (int)v;
            var first = (j & _mask) * _size;
            var second = ((j + 1) & _mask) * _size;
            var column = i & _mask;
            var nextColumn = (i + 1) & _mask;
            return new Address(first + column, first + nextColumn, second + column, second + nextColumn, u - i, v - j);
        }

        private static float Bilinear(float a, float b, float c, float d, float x, float y)
        {
            var top = a + (b - a) * x;
            var bottom = c + (d - c) * x;
            return top + (bottom - top) * y;
        }

        private static float Jonswap(float omega, float peak, float alpha, float gamma, float depth)
        {
            var ratio = peak / omega;
            var ratioSquared = ratio * ratio;
            var normalizedDepth = omega * MathF.Sqrt(depth / Gravity);
            var tma = normalizedDepth <= 1f ? .5f * normalizedDepth * normalizedDepth
                : normalizedDepth < 2f ? 1f - .5f * (2f - normalizedDepth) * (2f - normalizedDepth) : 1f;
            var sigma = omega <= peak ? .07f : .09f;
            var frequency = (omega - peak) / (peak * sigma);
            var enhancement = MathF.Exp(-.5f * frequency * frequency);
            var omegaSquared = omega * omega;
            return tma * alpha * Gravity * Gravity / (omegaSquared * omegaSquared * omega)
                * MathF.Exp(-1.25f * ratioSquared * ratioSquared) * MathF.Pow(gamma, enhancement);
        }

        private static float Directional(float angle, float wind, float ratio, float swell)
        {
            var basePower = ratio > 1f ? 9.77f * MathF.Pow(ratio, -2.5f) : 6.97f * MathF.Pow(ratio, 5f);
            var power = basePower + 16f * MathF.Tanh(Math.Min(ratio, 20f)) * swell * swell;
            var normalization = power < 5f
                ? (((-.000564f * power + .00776f) * power - .044f) * power + .192f) * power + .163f
                : (((-4.80e-08f * power + 1.07e-05f) * power - 9.53e-04f) * power + .059f) * power + .393f;
            var difference = (float)Math.IEEERemainder(angle - wind, TwoPi);
            var cosine = MathF.Cos(difference);
            var wide = (2f / MathF.PI) * cosine * cosine;
            var narrow = normalization * MathF.Pow(MathF.Abs(MathF.Cos(difference * .5f)), 2f * power);
            return .14f * wide + .86f * narrow;
        }

        private static Vector2 Gaussian(int seed, int cascade, int x, int z)
        {
            var key = unchecked((uint)seed * 0x9e3779b9u + (uint)cascade * 0xc2b2ae35u +
                (uint)x * 0x27d4eb2du + (uint)z * 0x165667b1u);
            var u = ((Hash(key) >> 8) + 1f) * (1f / 16_777_217f);
            var v = (Hash(key ^ 0x7f4a7c15u) >> 8) * (1f / 16_777_216f);
            var radius = MathF.Sqrt(-2f * MathF.Log(u));
            var (sine, cosine) = MathF.SinCos(TwoPi * v);
            return new Vector2(cosine, sine) * radius;
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value = unchecked(value * 0x7feb352du);
            value ^= value >> 15;
            value = unchecked(value * 0x846ca68bu);
            return value ^ (value >> 16);
        }
    }
}
