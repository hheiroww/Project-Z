using System.Runtime.InteropServices;
using System.Text;

namespace ProjectZ.StudioScenes;

internal sealed class OceanShaderProgram
{
    internal static readonly string[] Entries =
        ["InitializeCS", "EvolveCS", "RowsCS", "ColumnsCS", "ResolveCS", "RenderCS"];

    private static readonly Lazy<Task<byte[][]>> Compilation = new(() => Task.Run(Compile));

    internal static byte[][]? Ready(bool wait, out string? error)
    {
        var compilation = Compilation.Value;
        if (wait && !compilation.IsCompleted)
        {
            try { compilation.GetAwaiter().GetResult(); }
            catch (Exception failure) when (failure is InvalidDataException or DllNotFoundException
                or EntryPointNotFoundException or BadImageFormatException) { }
        }
        error = compilation.Exception?.GetBaseException().Message;
        return compilation.IsCompletedSuccessfully ? compilation.Result : null;
    }

    private static byte[][] Compile()
    {
        using var source = typeof(OceanShaderProgram).Assembly.GetManifestResourceStream("ProjectZ.StudioScenes.StudioOcean.hlsl")
            ?? throw new InvalidDataException("The embedded ocean shader is missing.");
        using var bytes = new MemoryStream();
        source.CopyTo(bytes);
        return Entries.Select(entry => CompileEntry(bytes.GetBuffer(), (int)bytes.Length, entry)).ToArray();
    }

    private static unsafe byte[] CompileEntry(byte[] source, int length, string entry)
    {
        fixed (byte* text = source)
        {
            const uint optimize = 1 << 15;
            int result = D3DCompile(text, (nuint)length, "StudioOcean.hlsl", 0, 0,
                entry, "cs_5_0", optimize, 0, out var shader, out var diagnostic);
            try
            {
                if (result < 0 || shader == 0)
                {
                    var message = diagnostic == 0 ? $"HRESULT 0x{result:X8}" : Encoding.UTF8.GetString(Blob(diagnostic)).TrimEnd('\0');
                    throw new InvalidDataException($"Direct3D could not compile {entry}: {message}");
                }
                return Blob(shader).ToArray();
            }
            finally
            {
                if (shader != 0) Marshal.Release(shader);
                if (diagnostic != 0) Marshal.Release(diagnostic);
            }
        }
    }

    private static unsafe ReadOnlySpan<byte> Blob(nint instance)
    {
        var table = *(nint**)instance;
        var buffer = (delegate* unmanaged[Stdcall]<nint, byte*>)table[3];
        var size = (delegate* unmanaged[Stdcall]<nint, nuint>)table[4];
        return new ReadOnlySpan<byte>(buffer(instance), checked((int)size(instance)));
    }

    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern unsafe int D3DCompile(byte* data, nuint size, string name, nint defines, nint include,
        string entry, string target, uint flags, uint effectFlags, out nint bytecode, out nint diagnostics);
}
