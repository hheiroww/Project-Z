using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ProjectZ;
using ProjectZ.ShaderEditor;
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
Reject(() => writeRun.Invoke(null, [effect, limitGraph + " "]), "worker rejects an oversized outgoing graph");
Reject(() => readRun.Invoke(null, [new byte[] { 0, 1, 4, 0, 5 }]), "worker rejects an oversized graph length in an incoming frame");

if (args.Contains("--desktop", StringComparer.Ordinal))
    await CheckDesktop(json);

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
