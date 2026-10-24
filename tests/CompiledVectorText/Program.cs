using ProjectZ.VectorText;

int checks = 0;
void Require(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
foreach (int weight in new[] { 400, 600, 700 })
{
    var font = CompiledFont.ForWeight(weight);
    Require(font.GlyphCount > 2800 && font.SupportsText("Café — Ελληνικά Русский 0123456789"), $"weight {weight}: Unicode outline coverage");
    Require(font.Measure("AV", 20).X < font.Measure("A", 20).X + font.Measure("V", 20).X, $"weight {weight}: kerning applied");
    Require(font.Measure("Cafe\u0301", 20) == font.Measure("Café", 20), $"weight {weight}: composed accents share layout");
    foreach (var (input, expected) in new[] { ("\uD83D", "\uFFFD"), ("\uDE00", "\uFFFD"), ("a\uD83Db", "a\uFFFDb"), ("a\uDE00b", "a\uFFFDb") })
        Require(font.SupportsText(input) == font.SupportsText(expected) && font.Measure(input, 20) == font.Measure(expected, 20)
            && font.GetTriangles(input, 20).SequenceEqual(font.GetTriangles(expected, 20)), $"weight {weight}: unpaired UTF-16 measures and tessellates as a replacement glyph");
    Require(font.Measure("a\r\nb", 20) == font.Measure("a\nb", 20), $"weight {weight}: CRLF and LF have identical layout");
    Require(font.Measure("ab\ncd", 20).Y == 2 * font.Measure("ab", 20).Y, $"weight {weight}: multiline height");
    var small = font.GetTriangles("AVOé", 20);
    var large = font.GetTriangles("AVOé", 40);
    Require(small.Length > 60 && small.Length % 6 == 0 && small.Length == large.Length && small.Zip(large).All(p => Math.Abs(p.First * 2 - p.Second) < .001), $"weight {weight}: real vector triangles scale continuously");
    Require(small.All(float.IsFinite), $"weight {weight}: finite mesh coordinates");
    var o = font.GetTriangles("O", 20);
    float x = (o.Where((_, i) => i % 2 == 0).Min() + o.Where((_, i) => i % 2 == 0).Max()) / 2;
    float y = (o.Where((_, i) => i % 2 == 1).Min() + o.Where((_, i) => i % 2 == 1).Max()) / 2;
    bool centerFilled = false;
    for (int i = 0; i < o.Length; i += 6)
    {
        float Cross(int a, int b) => (o[b] - o[a]) * (y - o[a + 1]) - (o[b + 1] - o[a + 1]) * (x - o[a]);
        var a = Cross(i, i + 2); var b = Cross(i + 2, i + 4); var c = Cross(i + 4, i);
        centerFilled |= (a >= 0 && b >= 0 && c >= 0) || (a <= 0 && b <= 0 && c <= 0);
    }
    Require(!centerFilled, $"weight {weight}: tessellation preserves the O counter");
    Require(font.GetTriangles(" \t\n", 20).Length == 0 && font.Measure("a ", 20).X > font.Measure("a", 20).X, $"weight {weight}: whitespace has advances without ink");
}
Require(!CompiledFont.ForWeight(400).SupportsText("العربية 日本語 😀"), "complex and uncovered text explicitly requests host shaping");
Require(!CompiledFont.ForWeight(400).GetTriangles("M", 20).SequenceEqual(CompiledFont.ForWeight(700).GetTriangles("M", 20)), "bold uses its own outlines");
Console.WriteLine($"PASS {checks} compiled-vector text checks");
