using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ProjectZ.ShaderEditor;

/// <summary>Private transport for the isolated Project-Z editor window.</summary>
public sealed class ShaderWorkerProcess
{
    private readonly Channel<bool> changes = Channel.CreateBounded<bool>(1);
    private string appearance;
    private byte[]? latest;
    private int closeRequested;

    public ShaderWorkerProcess(string? documentPath, string appearance)
    {
        this.appearance = appearance;
        Completion = RunAsync(documentPath);
    }

    public Task Completion { get; }

    public byte[]? TakeLatestBytecode() => Interlocked.Exchange(ref latest, null);

    public void RefreshAppearance(string settings)
    {
        Volatile.Write(ref appearance, settings);
        changes.Writer.TryWrite(true);
    }

    public void Close()
    {
        Volatile.Write(ref closeRequested, 1);
        changes.Writer.TryWrite(true);
    }

    private async Task RunAsync(string? documentPath)
    {
        string pipeName = PipeProtocol.Prefix + Guid.NewGuid().ToString("N");
        string basePath = AppContext.BaseDirectory;
        string worker = Path.Combine(basePath, "ProjectZ.ShaderEditor.dll");
        string runtimeConfig = Path.Combine(basePath, "ProjectZ.ShaderEditor.Worker.runtimeconfig.json");
        if (!File.Exists(worker) || !File.Exists(runtimeConfig))
            throw new FileNotFoundException("The Project-Z shader editor worker is missing from the application output.");

        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var stop = new CancellationTokenSource();
        var start = new ProcessStartInfo
        {
            FileName = ShaderCompiler.FindDotnetHost(),
            WorkingDirectory = basePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(runtimeConfig);
        string? application = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrEmpty(application))
        {
            string dependencies = Path.ChangeExtension(application, ".deps.json");
            if (File.Exists(dependencies))
            {
                start.ArgumentList.Add("--depsfile");
                start.ArgumentList.Add(dependencies);
            }
        }
        start.ArgumentList.Add(worker);
        start.ArgumentList.Add(pipeName);

        using var process = new Process { StartInfo = start };
        if (!process.Start())
            throw new InvalidOperationException("The Project-Z shader editor worker could not start.");
        var stderr = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExitAsync();
        var connected = pipe.WaitForConnectionAsync(stop.Token);
        if (await Task.WhenAny(connected, exited).ConfigureAwait(false) == exited && !connected.IsCompletedSuccessfully)
        {
            stop.Cancel();
            await IgnoreTransportEnd(connected).ConfigureAwait(false);
            throw await WorkerExitError(process, stderr).ConfigureAwait(false);
        }
        await connected.ConfigureAwait(false);
        var settings = new ShaderWorkerStart(documentPath, Volatile.Read(ref appearance));
        await PipeProtocol.WriteAsync(pipe, new(PipeMessage.Start, JsonSerializer.SerializeToUtf8Bytes(settings)), stop.Token)
            .ConfigureAwait(false);
        var sending = SendChangesAsync(pipe, stop.Token);
        var receiving = ReceiveResultsAsync(pipe, stop.Token);
        try
        {
            var first = await Task.WhenAny(exited, receiving, sending).ConfigureAwait(false);
            if (first != sending)
                await first.ConfigureAwait(false);
            await exited.ConfigureAwait(false);
            await receiving.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw await WorkerExitError(process, stderr).ConfigureAwait(false);
        }
        finally
        {
            stop.Cancel();
            await IgnoreTransportEnd(sending).ConfigureAwait(false);
        }
    }

    private async Task SendChangesAsync(Stream pipe, CancellationToken token)
    {
        await foreach (var _ in changes.Reader.ReadAllAsync(token).ConfigureAwait(false))
        {
            if (Volatile.Read(ref closeRequested) != 0)
            {
                await PipeProtocol.WriteAsync(pipe, new(PipeMessage.Close, []), token).ConfigureAwait(false);
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                return;
            }
            var settings = Encoding.UTF8.GetBytes(Volatile.Read(ref appearance));
            await PipeProtocol.WriteAsync(pipe, new(PipeMessage.Appearance, settings), token).ConfigureAwait(false);
        }
    }

    private async Task ReceiveResultsAsync(Stream pipe, CancellationToken token)
    {
        while (await PipeProtocol.ReadAsync(pipe, token).ConfigureAwait(false) is { } packet)
        {
            switch (packet.Type)
            {
                case PipeMessage.Bytecode:
                    Interlocked.Exchange(ref latest, packet.Payload);
                    break;
                case PipeMessage.Failure:
                    throw new InvalidOperationException(Encoding.UTF8.GetString(packet.Payload));
                default:
                    throw new InvalidDataException("Unexpected shader editor response.");
            }
        }
    }

    private static async Task<Exception> WorkerExitError(Process process, Task<string> stderr)
    {
        string details = (await stderr.ConfigureAwait(false)).Trim();
        string message = $"The Project-Z shader editor worker exited with code {process.ExitCode}.";
        return new InvalidOperationException(details.Length == 0 ? message : $"{message}{Environment.NewLine}{details}");
    }

    private static async Task IgnoreTransportEnd(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
    }
}

internal static class ShaderWorkerEntry
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !args[0].StartsWith(PipeProtocol.Prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(args[0].AsSpan(PipeProtocol.Prefix.Length), "N", out _))
            return 2;

        try
        {
            using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            pipe.Connect(15_000);
            var packet = PipeProtocol.ReadAsync(pipe, CancellationToken.None).GetAwaiter().GetResult();
            if (packet is not { Type: PipeMessage.Start })
                throw new InvalidDataException("The shader editor worker did not receive its startup settings.");
            var start = JsonSerializer.Deserialize<ShaderWorkerStart>(packet.Payload)
                ?? throw new InvalidDataException("The shader editor worker startup settings are empty.");
            return RunEditor(pipe, start);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int RunEditor(Stream pipe, ShaderWorkerStart start)
    {
        using var stop = new CancellationTokenSource();
        var results = Channel.CreateBounded<PipePacket>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        string appearance = start.Appearance;
        int closeRequested = 0;
        var reading = ReadCommands();
        var writing = WriteResults();
        Exception? failure = null;
        try
        {
            var context = AssemblyLoadContext.Default;
            context.Resolving += ResolveSibling;
            try
            {
                var framework = context.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "ProjectZ.dll"));
                var entry = framework.GetType("ProjectZ.Shaders", throwOnError: true)!
                    .GetMethod("RunEditorWorker", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException("ProjectZ.Shaders", "RunEditorWorker");
                Func<string> currentAppearance = () => Volatile.Read(ref appearance);
                Func<bool> shouldClose = () => Volatile.Read(ref closeRequested) != 0;
                Action<byte[]> publish = bytes => results.Writer.TryWrite(new(PipeMessage.Bytecode, bytes));
                entry.Invoke(null, [start.DocumentPath, currentAppearance, publish, shouldClose]);
            }
            finally
            {
                context.Resolving -= ResolveSibling;
            }
        }
        catch (Exception ex)
        {
            failure = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            results.Writer.TryWrite(new(PipeMessage.Failure, Encoding.UTF8.GetBytes(failure.ToString())));
        }
        finally
        {
            results.Writer.TryComplete();
            writing.GetAwaiter().GetResult();
            stop.Cancel();
            reading.GetAwaiter().GetResult();
        }
        return failure is null ? 0 : 1;

        async Task ReadCommands()
        {
            try
            {
                while (await PipeProtocol.ReadAsync(pipe, stop.Token).ConfigureAwait(false) is { } packet)
                {
                    if (packet.Type == PipeMessage.Close)
                        break;
                    if (packet.Type != PipeMessage.Appearance)
                        throw new InvalidDataException("Unexpected shader editor command.");
                    Volatile.Write(ref appearance, Encoding.UTF8.GetString(packet.Payload));
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
            finally { Volatile.Write(ref closeRequested, 1); }
        }

        async Task WriteResults()
        {
            try
            {
                await foreach (var packet in results.Reader.ReadAllAsync().ConfigureAwait(false))
                    await PipeProtocol.WriteAsync(pipe, packet, stop.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                Volatile.Write(ref closeRequested, 1);
            }
        }
    }

    private static Assembly? ResolveSibling(AssemblyLoadContext context, AssemblyName name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    }
}

internal sealed record ShaderWorkerStart(string? DocumentPath, string Appearance);
internal enum PipeMessage : byte { Start = 1, Appearance, Close, Bytecode, Failure }
internal sealed record PipePacket(PipeMessage Type, byte[] Payload);

internal static class PipeProtocol
{
    public const string Prefix = "ProjectZ.ShaderEditor.";
    private const int MaximumFrameLength = 64 * 1024 * 1024;

    public static async Task<PipePacket?> ReadAsync(Stream pipe, CancellationToken token)
    {
        var header = new byte[5];
        if (await pipe.ReadAsync(header.AsMemory(0, 1), token).ConfigureAwait(false) == 0)
            return null;
        await pipe.ReadExactlyAsync(header.AsMemory(1), token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
        if (length < 0 || length > MaximumFrameLength)
            throw new InvalidDataException("The shader editor sent an invalid message length.");
        var payload = new byte[length];
        await pipe.ReadExactlyAsync(payload, token).ConfigureAwait(false);
        return new PipePacket((PipeMessage)header[0], payload);
    }

    public static async Task WriteAsync(Stream pipe, PipePacket packet, CancellationToken token)
    {
        var header = new byte[5];
        header[0] = (byte)packet.Type;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), packet.Payload.Length);
        await pipe.WriteAsync(header, token).ConfigureAwait(false);
        await pipe.WriteAsync(packet.Payload, token).ConfigureAwait(false);
    }
}
