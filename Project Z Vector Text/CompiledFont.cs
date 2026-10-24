using System.Collections.Concurrent;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using LibTessDotNet;

namespace ProjectZ.VectorText;

/// <summary>ProjectZ Sans curves and metrics compiled into this assembly, without font containers or atlases.</summary>
public sealed class CompiledFont
{
    public const string FamilyName = "ProjectZ Sans";
    private static readonly Lazy<CompiledFont[]> Faces = new(ReadFaces);
    public static CompiledFont ForWeight(int weight) => Faces.Value[weight >= 650 ? 2 : weight >= 550 ? 1 : 0];
    private readonly float units, ascent;
    private readonly Glyph[] glyphs;
    private readonly Dictionary<int, ushort> characters;
    private readonly Kerning[] kerning;
    private readonly ConcurrentDictionary<ushort, float[]> triangles = new();
    public float LineHeight { get; }
    public int GlyphCount => glyphs.Length;

    private sealed class Glyph
    {
        internal ushort Advance;
        internal readonly List<(byte Kind, Vector2[] Points)> Commands = new();
    }
    private sealed class Kerning
    {
        internal readonly Dictionary<uint, short> Pairs = new();
        internal HashSet<ushort>? Coverage;
        internal ushort[] First = Array.Empty<ushort>(), Second = Array.Empty<ushort>();
        internal short[] Values = Array.Empty<short>();
        internal int Columns;
        internal bool TryGet(ushort a, ushort b, out short adjustment)
        {
            if (Coverage is null) return Pairs.TryGetValue(((uint)a << 16) | b, out adjustment);
            adjustment = 0;
            if (!Coverage.Contains(a)) return false;
            adjustment = Values[First[a] * Columns + Second[b]];
            return true;
        }
    }

    private CompiledFont(BinaryReader reader)
    {
        units = reader.ReadUInt16(); ascent = reader.ReadInt16();
        var descent = reader.ReadInt16(); var gap = reader.ReadInt16();
        LineHeight = (ascent - descent + gap) / units;
        glyphs = new Glyph[reader.ReadUInt16()];
        for (int i = 0; i < glyphs.Length; i++)
        {
            var glyph = glyphs[i] = new Glyph { Advance = reader.ReadUInt16() };
            int count = reader.ReadUInt16();
            for (int c = 0; c < count; c++)
            {
                byte kind = reader.ReadByte();
                int length = kind == 4 ? 0 : kind == 2 ? 3 : kind == 3 ? 2 : 1;
                var points = new Vector2[length];
                for (int p = 0; p < length; p++) points[p] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                glyph.Commands.Add((kind, points));
            }
        }
        int charactersCount = checked((int)reader.ReadUInt32());
        characters = new Dictionary<int, ushort>(charactersCount);
        for (int i = 0; i < charactersCount; i++) characters.Add(checked((int)reader.ReadUInt32()), reader.ReadUInt16());
        kerning = new Kerning[reader.ReadUInt16()];
        for (int k = 0; k < kerning.Length; k++)
        {
            var table = kerning[k] = new Kerning();
            if (reader.ReadByte() == 1)
            {
                int count = checked((int)reader.ReadUInt32());
                for (int i = 0; i < count; i++)
                {
                    uint a = reader.ReadUInt16(), b = reader.ReadUInt16();
                    table.Pairs.Add((a << 16) | b, reader.ReadInt16());
                }
            }
            else
            {
                int count = reader.ReadUInt16();
                table.Coverage = new HashSet<ushort>();
                for (int i = 0; i < count; i++) table.Coverage.Add(reader.ReadUInt16());
                table.First = new ushort[glyphs.Length]; table.Second = new ushort[glyphs.Length];
                for (int i = 0; i < glyphs.Length; i++) table.First[i] = reader.ReadUInt16();
                for (int i = 0; i < glyphs.Length; i++) table.Second[i] = reader.ReadUInt16();
                int rows = reader.ReadUInt16(); table.Columns = reader.ReadUInt16();
                table.Values = new short[rows * table.Columns];
                for (int i = 0; i < table.Values.Length; i++) table.Values[i] = reader.ReadInt16();
            }
        }
    }

    private static CompiledFont[] ReadFaces()
    {
        using var input = new MemoryStream(Convert.FromBase64String(string.Concat(OutlineData.Chunks)));
        using var zip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new BinaryReader(zip);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "PZV1") throw new InvalidDataException("Invalid compiled outline data");
        var faces = new CompiledFont[3];
        for (int i = 0; i < faces.Length; i++)
        {
            int length = checked((int)reader.ReadUInt32());
            using var face = new BinaryReader(new MemoryStream(reader.ReadBytes(length)));
            faces[i] = new CompiledFont(face);
            if (face.BaseStream.Position != length) throw new InvalidDataException("Invalid compiled outline length");
        }
        return faces;
    }

    public bool SupportsText(string text)
    {
        foreach (var cp in Codepoints(NormalizeText(text)))
        {
            if (cp is 9 or 10 or 13) continue;
            // Complex scripts retain the host's shaping and fallback instead of drawing isolated glyphs.
            if ((cp >= 0x590 && cp <= 0x1CFF) || (cp >= 0x300 && cp <= 0x36F) || !characters.ContainsKey(cp)) return false;
        }
        return true;
    }

    public Vector2 Measure(string text, float emSize, bool pixelPerfect = false) => Layout(text, emSize, pixelPerfect, null);

    public float[] GetTriangles(string text, float emSize, bool pixelPerfect = false)
    {
        var result = new List<float>();
        Layout(text, emSize, pixelPerfect, (id, x, y) =>
        {
            var mesh = triangles.GetOrAdd(id, Tessellate);
            for (int i = 0; i < mesh.Length; i += 2)
            {
                result.Add(x + mesh[i] * emSize / units);
                result.Add(y + (ascent - mesh[i + 1]) * emSize / units);
            }
        });
        return result.ToArray();
    }

    private Vector2 Layout(string text, float size, bool pixelPerfect, Action<ushort, float, float>? emit)
    {
        if (float.IsNaN(size) || float.IsInfinity(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        text = NormalizeText(text.Replace("\r\n", "\n").Replace('\r', '\n'));
        float x = 0, y = 0, width = 0, line = (float)Math.Ceiling(LineHeight * size);
        ushort previous = 0;
        foreach (int cp in Codepoints(text))
        {
            if (cp == '\n') { width = Math.Max(width, x); x = 0; y += line; previous = 0; continue; }
            if (cp == '\t')
            {
                float tab = glyphs[characters[32]].Advance * size / units * 4;
                x = ((float)Math.Floor(x / tab) + 1) * tab; previous = 0; continue;
            }
            ushort id = characters.TryGetValue(cp, out var mapped) ? mapped : (ushort)0;
            foreach (var table in kerning)
                if (table.TryGet(previous, id, out var adjustment)) { x += adjustment * size / units; break; }
            if (pixelPerfect) x = (float)Math.Round(x, MidpointRounding.AwayFromZero);
            emit?.Invoke(id, x, y);
            x += glyphs[id].Advance * size / units;
            if (pixelPerfect) x = (float)Math.Round(x, MidpointRounding.AwayFromZero);
            previous = id;
        }
        return new Vector2(Math.Max(width, x), y + line);
    }

    /// <summary>Canonical text with unpaired UTF-16 code units replaced, including transient caret prefixes.</summary>
    public static string NormalizeText(string text)
    {
        // Caret/wrapping measurements can end between an emoji's UTF-16 pair.
        // Replace only unpaired code units before NFC, preserving valid pairs.
        char[]? repaired = null;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsSurrogate(text[i])) continue;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { i++; continue; }
            (repaired ??= text.ToCharArray())[i] = '\uFFFD';
        }
        return (repaired is null ? text : new string(repaired)).Normalize(NormalizationForm.FormC);
    }

    private static IEnumerable<int> Codepoints(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                yield return char.ConvertToUtf32(c, text[++i]);
            else yield return char.IsSurrogate(c) ? 0xFFFD : c;
        }
    }

    private float[] Tessellate(ushort id)
    {
        var tess = new Tess();
        var contour = new List<Vector2>();
        Vector2 current = default;
        void Close()
        {
            if (contour.Count >= 3) tess.AddContour(contour.Select(p => new ContourVertex { Position = new Vec3(p.X, p.Y, 0) }).ToArray());
            contour.Clear();
        }
        foreach (var command in glyphs[id].Commands)
        {
            var p = command.Points;
            switch (command.Kind)
            {
                case 0: Close(); contour.Add(current = p[0]); break;
                case 1: contour.Add(current = p[0]); break;
                case 2: Cubic(contour, current, p[0], p[1], p[2], 0); current = p[2]; break;
                case 3:
                    Cubic(contour, current, current + (p[0] - current) * (2f / 3), p[1] + (p[0] - p[1]) * (2f / 3), p[1], 0);
                    current = p[1]; break;
                case 4: Close(); break;
            }
        }
        Close();
        tess.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3);
        var result = new List<float>();
        for (int i = 0; i < tess.ElementCount * 3; i += 3)
        {
            if (tess.Elements[i] < 0 || tess.Elements[i + 1] < 0 || tess.Elements[i + 2] < 0) continue;
            for (int j = 0; j < 3; j++)
            {
                var v = tess.Vertices[tess.Elements[i + j]].Position;
                result.Add(v.X); result.Add(v.Y);
            }
        }
        return result.ToArray();
    }

    private static void Cubic(List<Vector2> points, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int depth)
    {
        // De Casteljau subdivision in design units: retains curves at large and fractional display scales.
        var ab = (a + b) / 2; var bc = (b + c) / 2; var cd = (c + d) / 2;
        var abc = (ab + bc) / 2; var bcd = (bc + cd) / 2; var midpoint = (abc + bcd) / 2;
        if (depth >= 12 || (Vector2.DistanceSquared(b, (2 * a + d) / 3) < .01f && Vector2.DistanceSquared(c, (a + 2 * d) / 3) < .01f))
        { points.Add(d); return; }
        Cubic(points, a, ab, abc, midpoint, depth + 1);
        Cubic(points, midpoint, bcd, cd, d, depth + 1);
    }
}
