namespace ProjectZ.Vst;

internal static class ProfileFactory
{
    public static float[] Resample(IReadOnlyList<float> values, int length = GranularProfile.TableLength)
    {
        if (values.Count == 0)
            return new float[length];
        if (values.Count == 1)
            return Enumerable.Repeat(values[0], length).ToArray();

        var result = new float[length];
        for (var i = 0; i < length; i++)
        {
            var position = i * (values.Count - 1d) / Math.Max(1, length - 1);
            var first = (int)position;
            var second = Math.Min(first + 1, values.Count - 1);
            var fraction = (float)(position - first);
            result[i] = values[first] + ((values[second] - values[first]) * fraction);
        }
        Normalize(result);
        return result;
    }

    public static float[] MakeSource(float[] table, int sampleRate = 48000, int seconds = 4)
    {
        var result = new float[Math.Max(table.Length, sampleRate * seconds)];
        const double rootFrequency = 130.81278265; // C3, MIDI note 48
        var phase = 0d;
        for (var i = 0; i < result.Length; i++)
        {
            var drift = 0.006 * Math.Sin(Math.Tau * i / (sampleRate * 1.73));
            var increment = table.Length * rootFrequency * (1 + drift) / sampleRate;
            var index = (int)phase % table.Length;
            var next = (index + 1) % table.Length;
            var fraction = (float)(phase - Math.Floor(phase));
            result[i] = table[index] + ((table[next] - table[index]) * fraction);
            phase += increment;
            if (phase >= table.Length)
                phase -= table.Length;
        }
        return result;
    }

    public static void Normalize(float[] values)
    {
        if (values.Length == 0)
            return;
        var mean = values.Average(value => (double)value);
        var peak = 0d;
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (float)(values[i] - mean);
            peak = Math.Max(peak, Math.Abs(values[i]));
        }
        if (peak < 1e-9)
            return;
        var scale = (float)(0.98 / peak);
        for (var i = 0; i < values.Length; i++)
            values[i] *= scale;
    }
}
