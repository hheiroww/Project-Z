using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ProjectZ.ShaderEditor;

public enum ShaderBackend { DirectX12, DirectX11 }

public sealed record ShaderCompileResult(bool Success, byte[]? Bytecode, string Diagnostics, string SourcePath, string OutputPath);

public static class ShaderCompiler
{
    public static async Task<ShaderCompileResult> CompileAsync(string source, ShaderBackend backend,
        string toolDirectory, string stagingDirectory, CancellationToken cancellationToken = default)
    {
        string profile = backend switch
        {
            ShaderBackend.DirectX12 => "DirectX_12",
            ShaderBackend.DirectX11 => "DirectX_11",
            _ => throw new ArgumentOutOfRangeException(nameof(backend))
        };
        string staging = Path.GetFullPath(stagingDirectory);
        string sourcePath = Path.Combine(staging, $"shader.{backend}.fx");
        string outputPath = Path.Combine(staging, $"shader.{backend}.mgfx");
        string compilingPath = Path.Combine(staging, $"shader.{backend}.compiling.mgfx");
        string compilerPath = Path.GetFullPath(Path.Combine(toolDirectory, "mgfxc.dll"));
        if (!File.Exists(compilerPath))
            return Failed($"The MonoGame effect compiler is missing at {compilerPath}.");

        Directory.CreateDirectory(staging);
        using var stagingLock = await LockAsync(Path.Combine(staging, ".compile.lock"), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(sourcePath, source, cancellationToken).ConfigureAwait(false);
        // Start from an empty candidate so a previous compile cannot masquerade as this result.
        await File.WriteAllBytesAsync(compilingPath, [], cancellationToken).ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = FindDotnetHost(),
            WorkingDirectory = Path.GetDirectoryName(compilerPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(compilerPath);
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add(compilingPath);
        startInfo.ArgumentList.Add($"/Profile:{profile}");
        startInfo.ArgumentList.Add($"/Defines:{backend.ToString().ToUpperInvariant()}");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return Failed("The MonoGame effect compiler could not start.");
        }
        catch (Win32Exception exception)
        {
            return Failed($"The MonoGame effect compiler could not start with '{startInfo.FileName}'. Install or repair the .NET 8 runtime. {exception.Message}");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // It exited between checking HasExited and calling Kill.
            }
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            throw;
        }

        string diagnostics = string.Join(Environment.NewLine,
            new[] { await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false) }
                .Select(message => message.Trim()).Where(message => message.Length > 0));
        // MGFXC's DX12 path can write a container and exit zero after DXC rejects the shader.
        if (process.ExitCode != 0 || Regex.IsMatch(diagnostics, @"\berror(?:\s+[a-z]+\d+)?\s*:",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return Failed(diagnostics.Length > 0 ? diagnostics : $"MGFXC exited with code {process.ExitCode}.");

        byte[] bytecode = await File.ReadAllBytesAsync(compilingPath, cancellationToken).ConfigureAwait(false);
        if (!bytecode.AsSpan().StartsWith("MGFX"u8))
            return Failed($"MGFXC did not produce a valid effect.{Environment.NewLine}{diagnostics}".TrimEnd());
        File.Move(compilingPath, outputPath, overwrite: true);
        return new ShaderCompileResult(true, bytecode, diagnostics, sourcePath, outputPath);

        ShaderCompileResult Failed(string error) => new(false, null, error, sourcePath, outputPath);
    }

    private static async Task<FileStream> LockAsync(string path, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) == 32)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static string FindDotnetHost()
    {
        string? configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        string runtime = typeof(object).Assembly.Location;
        if (runtime.Length != 0)
        {
            string host = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(runtime)!, "..", "..", "..", "dotnet.exe"));
            if (File.Exists(host))
                return host;
        }
        return "dotnet";
    }
}
