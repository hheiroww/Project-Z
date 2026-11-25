using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ProjectZ;
using ProjectZ.ShaderEditor;
using ProjectZ.Shared.Drawing.ThreeD;
using ProjectZ.Shared.Drawing.UI.Primitives;
using ProjectZ.Windows.Composition;

var graph = ShaderDocument.Create();
graph.Mode = ShaderMode.Advanced;
var source = graph.Nodes.Single(node => node.Kind == "Source");
var output = graph.Nodes.Single(node => node.Kind == "Output");
var custom = ShaderNodes.Create("CustomFloat4", 260, 180);
custom.Expression = "float4(A.rgb * (sin(Time) * TexelSize.x + 1), A.a)";
graph.Nodes.Add(custom);
graph.Connections.Clear();
graph.Connections.Add(new() { Source = source.Id, Target = custom.Id, Input = "A" });
graph.Connections.Add(new() { Source = custom.Id, Target = output.Id, Input = "Color" });
string json = graph.ToJson();
var parsed = ShaderDocument.Parse(json);
Require(parsed.Mode == ShaderMode.Advanced && parsed.Nodes.Count == 3 && parsed.Connections.Count == 2, "canonical JSON preserves the authored graph");
Require(ShaderGenerator.Generate(parsed).Contains("technique XamlEffect", StringComparison.Ordinal), "existing MonoGame effect generation is available");
var raw = ShaderGenerator.GenerateVideoShader(parsed);
Require(!raw.Contains("technique", StringComparison.Ordinal) && raw.Contains("return float4(color.rgb * alpha, alpha);", StringComparison.Ordinal), "video emits a standalone premultiplied pixel shader");
CompilePixelShader(raw);
Console.WriteLine("PASS D3DCompile accepts ProjectZVideoShader, including source texture and time/texel-size parameters");

Reject(() => ShaderDocument.Parse(json + new string(' ', ShaderDocument.MaximumGraphBytes)), "graph parser enforces the UTF-8 transfer cap");
Reject(() => ShaderDocument.Parse("{\"Nodes\": [], \"Connections\": []}"), "graph parser runs the existing validation");
var limitGraph = json.PadRight(ShaderDocument.MaximumGraphBytes);
Require(ShaderDocument.Parse(limitGraph).Mode == ShaderMode.Advanced, "graph parser accepts the exact limit");
Reject(() => ShaderDocument.Parse(limitGraph[..^1] + "é"), "graph transfer cap counts UTF-8 bytes");

var protocol = typeof(ShaderWorkerProcess).Assembly.GetType("ProjectZ.ShaderEditor.PipeProtocol", throwOnError: true)!;
var writeRun = protocol.GetMethod("RunResult")!;
var readRun = protocol.GetMethod("ReadRunResult")!;
byte[] effect = [0x4d, 0x47, 0x46, 0x58];
var packet = writeRun.Invoke(null, [effect, limitGraph])!;
var payload = (byte[])packet.GetType().GetProperty("Payload")!.GetValue(packet)!;
var result = ((byte[] Bytecode, string? Graph))readRun.Invoke(null, [payload])!;
Require(result.Graph == limitGraph && result.Bytecode.SequenceEqual(effect), "a worker frame carries both results for the same successful Run");
var effectOnly = writeRun.Invoke(null, [effect, null])!;
payload = (byte[])effectOnly.GetType().GetProperty("Payload")!.GetValue(effectOnly)!;
result = ((byte[] Bytecode, string? Graph))readRun.Invoke(null, [payload])!;
Require(result.Graph is null && result.Bytecode.SequenceEqual(effect), "worker can transfer a compiled effect without graph data");
var graphOnly = writeRun.Invoke(null, [Array.Empty<byte>(), json])!;
payload = (byte[])graphOnly.GetType().GetProperty("Payload")!.GetValue(graphOnly)!;
result = ((byte[] Bytecode, string? Graph))readRun.Invoke(null, [payload])!;
Require(result.Graph == json && result.Bytecode.Length == 0, "hosted Apply delivers its graph without waiting for preview compilation");
Reject(() => writeRun.Invoke(null, [effect, limitGraph + " "]), "worker rejects an oversized outgoing graph");
Reject(() => readRun.Invoke(null, [new byte[] { 0, 1, 4, 0, 5 }]), "worker rejects an oversized graph length in an incoming frame");

var presetTool = Environment.GetEnvironmentVariable("PROJECTZ_PRESET_TEST_COMPILER");
var presetStage = Environment.GetEnvironmentVariable("PROJECTZ_PRESET_TEST_STAGE");
await CheckPresets(presetTool, presetStage);
await CheckSurfaceMaterials(presetTool, presetStage);

if (args.Contains("--desktop", StringComparer.Ordinal))
{
    await CheckDesktop(json);
    await CheckHostedDesktop();
}

static async Task CheckPresets(string? compiler, string? staging)
{
    Require(ShaderPresets.MatchesSearch("water-clear", "Water", "Clear Pool", "Surface material") &&
            ShaderPresets.MatchesSearch("SOFT glow", "Soft Glow") &&
            !ShaderPresets.MatchesSearch("water bloom", "Clear Pool", "Water"),
        "shader search matches each term across labels and ignores punctuation and case");
    var presets = ShaderPresets.All;
    Require(presets.Count >= 50 && presets.Select(preset => preset.Id).Distinct(StringComparer.Ordinal).Count() == presets.Count,
        "the catalog provides at least 50 presets with distinct identities");
    if ((compiler is null) != (staging is null))
        throw new ArgumentException("Set both PROJECTZ_PRESET_TEST_COMPILER and PROJECTZ_PRESET_TEST_STAGE to compile MonoGame effects.");
    if (compiler is not null && (!File.Exists(Path.Combine(compiler, "mgfxc.dll")) || !Path.IsPathFullyQualified(staging!)))
        throw new ArgumentException("Preset verification requires an installed compiler and an absolute staging directory.");

    var variants = new HashSet<string>(StringComparer.Ordinal);
    foreach (var preset in presets)
    {
        try
        {
            var document = preset.CreateDocument();
            var clone = preset.CreateDocument();
            string json = document.ToJson();
            if (document.Name != preset.Name || document.Nodes.Count < 3 || !document.Connections.Any() ||
                ShaderDocument.Parse(json).ToJson() != json || document.Nodes.Intersect(clone.Nodes).Any() ||
                document.Nodes.Select(node => node.Id).Intersect(clone.Nodes.Select(node => node.Id)).Any())
                throw new InvalidOperationException("Expected a valid editable graph, independent copies, and an exact JSON round trip.");

            var video = ShaderGenerator.GenerateVideoShader(document);
            if (!variants.Add(video)) throw new InvalidOperationException("Two presets produce identical video shaders.");
            CompilePixelShader(video);
            if (compiler is not null)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                string effectSource = ShaderGenerator.Generate(document);
                foreach (var backend in Enum.GetValues<ShaderBackend>())
                {
                    var result = await ShaderCompiler.CompileAsync(effectSource, backend, compiler, staging!, deadline.Token);
                    if (!result.Success) throw new InvalidOperationException($"{backend}: {result.Diagnostics}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Preset '{preset.Id}' failed: {ex.Message}", ex);
        }
    }
    Console.WriteLine($"PASS {presets.Count} editable preset graphs and native video shaders" +
        (compiler is null ? "" : $", plus {presets.Count * 2} MonoGame DirectX 11/12 effect compilations"));
}

static async Task CheckSurfaceMaterials(string? compiler, string? staging)
{
    var presets = SurfaceMaterials.Presets;
    Require(presets.Count == 8 && presets.Select(preset => preset.Id).Distinct().Count() == presets.Count,
        "eight surface material presets have stable and unique IDs");
    foreach (var preset in presets)
    {
        var document = SurfaceMaterials.CreatePreset(preset.Id);
        Require(document.IsSurface && document.Mode == ShaderMode.Simple &&
                SurfaceMaterials.Channels.All(channel => SurfaceMaterials.FindConstant(document, channel) is not null),
            $"{preset.Name} opens with every Simple input connected to an editable node");
        string json = document.ToJson();
        var advanced = ShaderDocument.Parse(json);
        advanced.Mode = ShaderMode.Advanced;
        Require(SurfaceMaterials.GenerateHlsl(advanced) == SurfaceMaterials.GenerateHlsl(document) &&
                ShaderDocument.Parse(json).ToJson() == json,
            $"{preset.Name} round-trips the graph and renders the same in both views");
        CompilePixelShader(SurfaceMaterials.GenerateHlsl(advanced) + """

            float4 ProjectZVideoShader(float2 uv : TEXCOORD0) : SV_Target
            {
                SurfaceChannels material = EvaluateSurfaceMaterial(uv, 1.0);
                return float4(material.BaseColor * material.Transmission + material.Emission + material.Absorption,
                    material.IndexOfRefraction);
            }
            """);
        if (compiler is not null)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            string effectSource = ShaderGenerator.Generate(document);
            foreach (var backend in Enum.GetValues<ShaderBackend>())
            {
                var result = await ShaderCompiler.CompileAsync(effectSource, backend, compiler, staging!, deadline.Token);
                if (!result.Success) throw new InvalidOperationException($"{preset.Id} {backend}: {result.Diagnostics}");
            }
        }
    }

    var water = SurfaceMaterials.CreatePreset("clear-water");
    var material = new SurfaceMaterial3D();
    Require(material.ShaderGraph is null && material.GenerateShaderSource() is null,
        "legacy diffuse materials still have no material shader");
    material.ShaderGraph = water.ToJson();
    var frozen = material.ShaderGraph;
    var source = material.GenerateShaderSource();
    water.Name = "Changed source graph";
    Require(material.ShaderGraph == frozen && material.GenerateShaderSource() == source && source == SurfaceMaterials.GenerateHlsl(water),
        "a surface material retains its validated snapshot when the input graph is edited");
    Reject(() => material.ShaderGraph = ShaderDocument.Create().ToJson(), "surface materials reject image-effect graphs");
    Require(material.ShaderGraph == frozen && material.GenerateShaderSource() == source, "rejected material updates preserve the current shader");
    material.ShaderGraph = null;
    Require(material.GenerateShaderSource() is null, "clearing a graph restores diffuse rendering");
    var names = SurfaceMaterials.Channels.ToDictionary(channel => channel.Name);
    Require(SurfaceMaterials.FindConstant(water, names["IndexOfRefraction"])!.Values[0] == 1.333 &&
            SurfaceMaterials.FindConstant(water, names["AmbientOcclusion"])!.Values[0] > 0,
        "Clear Water enables refraction and ambient occlusion by default");
    var roughness = SurfaceMaterials.FindConstant(water, names["Roughness"])!;
    var output = water.Nodes.Single(node => node.Kind == "SurfaceOutput");
    var driven = ShaderNodes.Create("CustomFloat", 100, 400);
    driven.Expression = "A * (1 + 0.1 * sin(Time + uv.x))";
    water.Nodes.Add(driven);
    water.Connections.RemoveAll(wire => wire.Target == output.Id && wire.Input == "Roughness");
    water.Connections.Add(new() { Source = roughness.Id, Target = driven.Id, Input = "A" });
    water.Connections.Add(new() { Source = driven.Id, Target = output.Id, Input = "Roughness" });
    Require(SurfaceMaterials.FindConstant(water, names["Roughness"]) is null,
        "Simple leaves an Advanced-driven channel intact");
    CompilePixelShader(SurfaceMaterials.GenerateHlsl(water) + """

        float4 ProjectZVideoShader(float2 uv : TEXCOORD0) : SV_Target
        {
            return float4(EvaluateSurfaceMaterial(uv, 1.0).Roughness, 0, 0, 1);
        }
        """);
    Reject(() => ShaderGenerator.GenerateVideoShader(water), "material output requires a surface renderer");
}

static async Task CheckDesktop(string json)
{
    const string title = "Shader Editor · Project-Z";
    Require(Path.IsPathFullyQualified(Environment.GetEnvironmentVariable("PROJECTZ_SHADER_EDITOR_STAGING") ?? ""),
        "desktop compiler uses an explicitly owned staging path");
    Require(FindWindow(null, title) == IntPtr.Zero, "no other shader editor owns the desktop fixture title");
    var appearance = new WindowAppearance();
    appearance.Update(new() { DwmBlurEnabled = false });
    var session = Shaders.ShowEditorForGraph(json, appearance);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
    try
    {
        while (!timeout.IsCancellationRequested)
        {
            if (session.Completion.IsCompleted)
            {
                await session.Completion;
                throw new InvalidOperationException("The shader editor closed before Run.");
            }
            var window = FindWindow(null, title);
            if (window != IntPtr.Zero)
            {
                PostMessage(window, 0x100, 0x11, 1);
                PostMessage(window, 0x100, 0x0d, 1);
                PostMessage(window, 0x101, 0x0d, 1);
                PostMessage(window, 0x101, 0x11, 1);
            }
            string returned = null!;
            if (session.TryTakeGraph(ref returned))
            {
                Require(ShaderDocument.Parse(returned).ToJson() == json, "Run returns the exact graph opened as a draft");
                XamlNativeEffect effect = null!;
                Require(session.TryTakeEffect(ref effect), "the same Run remains available as a visual effect");
                effect.Dispose();
                PostMessage(window, 0x0010, 0, 0);
                await session.Completion.WaitAsync(TimeSpan.FromSeconds(8));
                Require(FindWindow(null, title) == IntPtr.Zero, "system close exits a clean standalone editor");
                return;
            }
            await Task.Delay(350, timeout.Token);
        }
        throw new TimeoutException("The shader editor did not publish a successful Run.");
    }
    finally
    {
        session.Close();
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
    }
}

static async Task CheckHostedDesktop()
{
    const string title = "Shader Editor · Project-Z";
    string original = SurfaceMaterials.CreatePreset("pool-water").ToJson();
    var appearance = new WindowAppearance();
    appearance.Update(new() { DwmBlurEnabled = false });
    var session = Shaders.ShowSurfaceEditorForGraph(original, appearance);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
    try
    {
        var window = IntPtr.Zero;
        while (window == IntPtr.Zero)
        {
            if (session.Completion.IsCompleted) await session.Completion;
            window = FindWindow(null, title);
            if (window == IntPtr.Zero) await Task.Delay(100, timeout.Token);
        }
        SendChord(window, 0x4e);
        await Task.Delay(500, timeout.Token);
        string applied = null!;
        while (!session.TryTakeGraph(ref applied))
        {
            SendChord(window, 0x0d);
            await Task.Delay(350, timeout.Token);
        }
        Require(applied != original && ShaderDocument.Parse(applied).Name == "Clear Water",
            "hosted Apply returns the changed material graph");
        session.AcknowledgeGraph(applied);
        await Task.Delay(500, timeout.Token);
        PostMessage(window, 0x0010, 0, 0);
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(8));
        Require(FindWindow(null, title) == IntPtr.Zero, "host acknowledgment clears the dirty state and allows system close");
    }
    finally
    {
        session.Close();
        await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
    }

    static void SendChord(IntPtr window, nuint key)
    {
        PostMessage(window, 0x100, 0x11, 1);
        PostMessage(window, 0x100, key, 1);
        PostMessage(window, 0x101, key, 1);
        PostMessage(window, 0x101, 0x11, 1);
    }
}

static void CompilePixelShader(string source)
{
    var input = Encoding.UTF8.GetBytes(source);
    int hr = D3DCompile(input, (nuint)input.Length, null, IntPtr.Zero, IntPtr.Zero,
        "ProjectZVideoShader", "ps_4_0", 1u << 11, 0, out var shader, out var errors);
    try
    {
        if (hr < 0)
        {
            var details = errors == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(GetBlobPointer(errors));
            throw new InvalidOperationException($"D3DCompile returned 0x{hr:X8}: {details}");
        }
    }
    finally
    {
        if (shader != IntPtr.Zero) Marshal.Release(shader);
        if (errors != IntPtr.Zero) Marshal.Release(errors);
    }
}

static IntPtr GetBlobPointer(IntPtr blob)
{
    var address = Marshal.ReadIntPtr(Marshal.ReadIntPtr(blob), 3 * IntPtr.Size);
    return Marshal.GetDelegateForFunctionPointer<BlobPointer>(address)(blob);
}

static void Reject(Action action, string label)
{
    try { action(); }
    catch (InvalidDataException) { Console.WriteLine("PASS " + label); return; }
    catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { Console.WriteLine("PASS " + label); return; }
    throw new InvalidOperationException(label);
}

static void Require(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    Console.WriteLine("PASS " + label);
}

[DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)]
static extern int D3DCompile(byte[] source, nuint length, string? name, IntPtr defines, IntPtr include,
    string entry, string profile, uint flags, uint effects, out IntPtr shader, out IntPtr errors);

[DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
static extern IntPtr FindWindow(string? windowClass, string title);

[DllImport("user32.dll", EntryPoint = "PostMessageW")]
static extern bool PostMessage(IntPtr window, uint message, nuint wParam, nint lParam);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
delegate IntPtr BlobPointer(IntPtr blob);
