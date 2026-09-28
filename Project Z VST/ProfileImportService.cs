using NAudio.Wave;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectZ.Vst;

internal static class ProfileImportService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".wave", ".mp3", ".mp2", ".aif", ".aiff", ".wma", ".flac", ".ogg", ".opus", ".aac", ".m4a"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".csv", ".yaml", ".yml", ".ini", ".log", ".rtf",
        ".html", ".htm", ".css", ".js", ".ts", ".cs", ".vb", ".py", ".prompt"
    };

    public static Task<GranularProfile> FromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("The dropped file does not exist.", path);

        var extension = Path.GetExtension(path);
        if (AudioExtensions.Contains(extension))
            return FromAudioAsync(path, cancellationToken);
        if (ImageExtensions.Contains(extension))
            return FromImageAsync(path, cancellationToken);
        if (TextExtensions.Contains(extension))
            return FromTextFileAsync(path, cancellationToken);
        return Task.Run(() => FromBinary(path, cancellationToken), cancellationToken);
    }

    public static Task<GranularProfile> FromAudioAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => FromAudio(path, cancellationToken), cancellationToken);

    public static Task<GranularProfile> FromImageAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => FromImage(path, cancellationToken), cancellationToken);

    public static Task<GranularProfile> FromTextFileAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => FromText(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path), cancellationToken), cancellationToken);

    public static GranularProfile FromText(string name, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("Text input is empty.");

        var runes = text.EnumerateRunes().Select(rune => (float)((rune.Value % 1024) / 511.5 - 1)).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var table = ProfileFactory.Resample(runes);
        Smooth(table, 5);
        ProfileFactory.Normalize(table);
        return new GranularProfile(name, table, ProfileFactory.MakeSource(table), 105, 18, 45, 34, -2, 72, 74, 100, -2);
    }

    private static GranularProfile FromAudio(string path, CancellationToken cancellationToken)
    {
        using var reader = new AudioFileReader(path);
        var channels = reader.WaveFormat.Channels;
        var maxFrames = reader.WaveFormat.SampleRate * 120;
        var mono = new List<float>(Math.Min(maxFrames, 4_000_000));
        var buffer = new float[8192 * channels];
        while (mono.Count < maxFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            for (var i = 0; i + channels <= read && mono.Count < maxFrames; i += channels)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                    sum += buffer[i + channel];
                mono.Add(sum / channels);
            }
        }

        if (mono.Count < 2)
            throw new InvalidDataException("The audio file contains no decodable samples.");
        var samples = mono.ToArray();
        ProfileFactory.Normalize(samples);
        var table = ProfileFactory.Resample(samples);
        return new GranularProfile(Path.GetFileNameWithoutExtension(path), table, samples, 78, 26, 50, 22, 0, 48, 65, 100, -1, reader.WaveFormat.SampleRate);
    }

    private static GranularProfile FromImage(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource bitmap = decoder.Frames[0];
        if (bitmap.Format != PixelFormats.Bgra32)
            bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var columns = new float[bitmap.PixelWidth];
        for (var x = 0; x < bitmap.PixelWidth; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double weighted = 0;
            double weight = 0;
            for (var y = 0; y < bitmap.PixelHeight; y++)
            {
                var offset = (y * stride) + (x * 4);
                var alpha = pixels[offset + 3] / 255d;
                var luminance = ((pixels[offset + 2] * 0.2126) + (pixels[offset + 1] * 0.7152) + (pixels[offset] * 0.0722)) / 255d;
                var verticalWeight = 0.25 + (0.75 * (1 - y / (double)Math.Max(1, bitmap.PixelHeight - 1)));
                weighted += ((luminance * 2) - 1) * alpha * verticalWeight;
                weight += Math.Max(0.05, alpha * verticalWeight);
            }
            columns[x] = (float)(weighted / weight);
        }

        var table = ProfileFactory.Resample(columns);
        Smooth(table, 3);
        ProfileFactory.Normalize(table);
        return new GranularProfile(Path.GetFileNameWithoutExtension(path), table, ProfileFactory.MakeSource(table), 120, 16, 52, 42, -5, 82, 85, 100, -3);
    }

    private static GranularProfile FromBinary(string path, CancellationToken cancellationToken)
    {
        const int maximumBytes = 4_000_000;
        using var stream = File.OpenRead(path);
        if (stream.Length == 0)
            throw new InvalidDataException("The dropped file is empty.");

        var length = (int)Math.Min(stream.Length, maximumBytes);
        var bytes = new byte[length];
        var offset = 0;
        while (offset < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0)
                break;
            offset += read;
        }

        var values = bytes.AsSpan(0, offset).ToArray()
            .Select(value => (float)((value - 127.5) / 127.5))
            .ToArray();
        var table = ProfileFactory.Resample(values);
        Smooth(table, 2);
        ProfileFactory.Normalize(table);
        return new GranularProfile(
            Path.GetFileNameWithoutExtension(path), table, ProfileFactory.MakeSource(table),
            92, 20, 50, 36, 0, 68, 72, 100, -2);
    }

    private static void Smooth(float[] values, int passes)
    {
        var scratch = new float[values.Length];
        for (var pass = 0; pass < passes; pass++)
        {
            for (var i = 0; i < values.Length; i++)
                scratch[i] = (values[(i + values.Length - 1) % values.Length] + (2 * values[i]) + values[(i + 1) % values.Length]) * 0.25f;
            Array.Copy(scratch, values, values.Length);
        }
    }
}
