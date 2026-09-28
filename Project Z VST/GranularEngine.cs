using System.Runtime.CompilerServices;

namespace ProjectZ.Vst;

internal readonly record struct GranularSettings(
    float GrainSizeMs,
    float Density,
    float Position,
    float Spray,
    float PitchSemitones,
    float TableDepth,
    float StereoSpread,
    float Level,
    float OutputDb,
    bool Hold,
    bool Mute);

internal sealed class GranularEngine
{
    private const int MaxGrains = 128;
    private const int MidiNoteCount = 128;
    private const int RootMidiNote = 48;
    private readonly Grain[] _grains = new Grain[MaxGrains];
    private readonly bool[] _heldNotes = new bool[MidiNoteCount];
    private readonly float[] _noteVelocities = new float[MidiNoteCount];
    private GranularProfile _profile = GranularProfile.Default;
    private float _sampleRate = 48000;
    private double _samplesUntilNextGrain;
    private double _modulationPhase;
    private uint _randomState = 0x5A17B19Du;
    private int _noteCursor = RootMidiNote;
    private int _activeNoteCount;

    public GranularProfile Profile => Volatile.Read(ref _profile);

    public void Configure(double sampleRate)
    {
        _sampleRate = (float)Math.Clamp(sampleRate, 8000, 384000);
        _samplesUntilNextGrain = 0;
        Array.Clear(_grains);
    }

    public void SetProfile(GranularProfile profile) => Volatile.Write(ref _profile, profile);

    public void NoteOn(int noteNumber, float velocity)
    {
        if ((uint)noteNumber >= MidiNoteCount)
            return;
        if (velocity <= 0)
        {
            NoteOff(noteNumber);
            return;
        }
        if (!_heldNotes[noteNumber])
            _activeNoteCount++;
        _heldNotes[noteNumber] = true;
        _noteVelocities[noteNumber] = Math.Clamp(velocity, 0, 1);
        _noteCursor = noteNumber;
        if (_activeNoteCount == 1)
            _samplesUntilNextGrain = 0;
    }

    public void NoteOff(int noteNumber)
    {
        if ((uint)noteNumber >= MidiNoteCount || !_heldNotes[noteNumber])
            return;
        _heldNotes[noteNumber] = false;
        _noteVelocities[noteNumber] = 0;
        _activeNoteCount = Math.Max(0, _activeNoteCount - 1);
    }

    public void AllNotesOff()
    {
        Array.Clear(_heldNotes);
        Array.Clear(_noteVelocities);
        _activeNoteCount = 0;
    }

    public void Process(Span<float> outputLeft, Span<float> outputRight, in GranularSettings settings)
    {
        var count = Math.Min(outputLeft.Length, outputRight.Length);
        if (settings.Mute)
        {
            outputLeft[..count].Clear();
            outputRight[..count].Clear();
            return;
        }

        var profile = Volatile.Read(ref _profile);
        var source = profile.SourceSamples is { Length: > 1 } samples ? samples : profile.Wavetable;
        var level = Math.Clamp(settings.Level * 0.01f, 0, 1);
        var outputGain = MathF.Pow(10f, settings.OutputDb / 20f) * level;
        var tableDepth = Math.Clamp(settings.TableDepth * 0.01f, 0, 1);
        var density = Math.Clamp(settings.Density, 1, 160);
        var interval = _sampleRate / density;

        for (var i = 0; i < count; i++)
        {
            if (_activeNoteCount > 0 && _samplesUntilNextGrain <= 0)
            {
                SpawnGrain(profile, source, settings, tableDepth);
                _samplesUntilNextGrain += interval;
            }
            _samplesUntilNextGrain -= 1;

            var wetLeft = 0f;
            var wetRight = 0f;
            for (var grainIndex = 0; grainIndex < _grains.Length; grainIndex++)
            {
                ref var grain = ref _grains[grainIndex];
                if (!grain.Active)
                    continue;

                var phase = grain.Age / grain.Duration;
                if (phase >= 1)
                {
                    grain.Active = false;
                    continue;
                }

                var window = 0.5f - (0.5f * MathF.Cos(MathF.Tau * phase));
                var sample = ReadCircular(source, grain.SourcePosition);
                wetLeft += sample * window * grain.LeftGain;
                wetRight += sample * window * grain.RightGain;
                grain.SourcePosition += grain.SourceIncrement;
                grain.Age += 1;
            }

            outputLeft[i] = wetLeft * outputGain;
            outputRight[i] = wetRight * outputGain;
        }
    }

    private void SpawnGrain(GranularProfile profile, float[] source, in GranularSettings settings, float tableDepth)
    {
        var slot = FindFreeGrain();
        var note = FindNextNote();
        if (slot < 0 || note < 0)
            return;

        var table = profile.Wavetable;
        var tableIndex = table.Length == 0 ? 0 : (int)(_modulationPhase * table.Length) % table.Length;
        var modulation = table.Length == 0 ? 0 : Math.Clamp(table[tableIndex], -1, 1);
        _modulationPhase += 0.61803398875 / Math.Max(1, settings.Density);
        _modulationPhase -= Math.Floor(_modulationPhase);

        var durationMs = Math.Clamp(settings.GrainSizeMs * (1 + (modulation * tableDepth * 0.7f)), 5, 600);
        var duration = Math.Max(16, durationMs * _sampleRate / 1000f);
        var pitch = (note - RootMidiNote) + settings.PitchSemitones + (modulation * tableDepth * 12f);
        var sampleRateCorrection = profile.SourceSampleRate / _sampleRate;
        var increment = MathF.Pow(2, pitch / 12f) * sampleRateCorrection;
        var position = Math.Clamp(settings.Position * 0.01f, 0, 1);
        var sourcePosition = position * (source.Length - 1d);
        var spraySamples = settings.Spray * 0.01 * source.Length * 0.25;
        sourcePosition = Wrap(sourcePosition + (NextBipolar() * spraySamples), source.Length);
        var pan = NextBipolar() * Math.Clamp(settings.StereoSpread * 0.01f, 0, 1);
        var angle = (pan + 1) * MathF.PI * 0.25f;
        var velocity = _noteVelocities[note];

        _grains[slot] = new Grain
        {
            Active = true,
            Duration = duration,
            SourcePosition = sourcePosition,
            SourceIncrement = increment,
            LeftGain = MathF.Cos(angle) * velocity * 0.36f,
            RightGain = MathF.Sin(angle) * velocity * 0.36f
        };
    }

    private int FindFreeGrain()
    {
        for (var i = 0; i < _grains.Length; i++)
            if (!_grains[i].Active)
                return i;
        return -1;
    }

    private int FindNextNote()
    {
        if (_activeNoteCount == 0)
            return -1;
        for (var offset = 1; offset <= MidiNoteCount; offset++)
        {
            var note = (_noteCursor + offset) % MidiNoteCount;
            if (!_heldNotes[note])
                continue;
            _noteCursor = note;
            return note;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadCircular(float[] source, double position)
    {
        position = Wrap(position, source.Length);
        var first = (int)position;
        var second = first + 1 == source.Length ? 0 : first + 1;
        var fraction = (float)(position - first);
        return source[first] + ((source[second] - source[first]) * fraction);
    }

    private float NextBipolar()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return ((_randomState & 0xFFFFFF) / 8388607.5f) - 1f;
    }

    private static double Wrap(double value, int length)
    {
        value %= length;
        return value < 0 ? value + length : value;
    }

    private struct Grain
    {
        public bool Active;
        public float Duration;
        public float Age;
        public double SourcePosition;
        public float SourceIncrement;
        public float LeftGain;
        public float RightGain;
    }
}
