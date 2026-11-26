using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ProjectZ.ShaderEditor;

/// <summary>Private transport for the isolated Project-Z editor window.</summary>
public sealed class ShaderWorkerProcess
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
    private readonly Channel<bool> changes = Channel.CreateBounded<bool>(1);
    private readonly ConcurrentQueue<ShaderGraphReceipt> receipts = new();
    private readonly TaskCompletionSource closeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object resultLock = new();
    private string appearance;
    private byte[]? latestBytecode;
    private string? latestGraph;
    private int closeRequested;
    private int activationRequested;
    private int childProcessId;
    private long windowHandle;

    public ShaderWorkerProcess(string? documentPath, string appearance) : this(documentPath, appearance, null) { }

    public ShaderWorkerProcess(string? documentPath, string appearance, string? graphJson) :
        this(documentPath, appearance, graphJson, false, false) { }

    public ShaderWorkerProcess(string? documentPath, string appearance, string? graphJson, bool hosted, bool surface) :
        this(documentPath, appearance, graphJson, hosted, surface, 0, null) { }

    public ShaderWorkerProcess(string? documentPath, string appearance, string? graphJson, bool hosted, bool surface,
        long ownerHandle, string? title) : this(documentPath, appearance, graphJson, hosted, surface, ownerHandle, title, false) { }

    public ShaderWorkerProcess(string? documentPath, string appearance, string? graphJson, bool hosted, bool surface,
        long ownerHandle, string? title, bool realtime)
    {
        this.appearance = appearance;
        Completion = RunAsync(documentPath, graphJson, hosted, surface, ownerHandle, title, realtime);
    }

    public Task Completion { get; }
    public long WindowHandle => Interlocked.Read(ref windowHandle);

    public byte[]? TakeLatestBytecode()
    {
        lock (resultLock)
        {
            var result = latestBytecode;
            latestBytecode = null;
            return result;
        }
    }

    public string? TakeLatestGraph()
    {
        lock (resultLock)
        {
            var result = latestGraph;
            latestGraph = null;
            return result;
        }
    }

    public void RefreshAppearance(string settings)
    {
        Volatile.Write(ref appearance, settings);
        changes.Writer.TryWrite(true);
    }

    public void Close()
    {
        Volatile.Write(ref closeRequested, 1);
        changes.Writer.TryWrite(true);
        closeSignal.TrySetResult();
    }

    public void Activate()
    {
        if (Completion.IsCompleted || Volatile.Read(ref closeRequested) != 0)
            return;
        int processId = Volatile.Read(ref childProcessId);
        if (OperatingSystem.IsWindows() && processId != 0)
            AllowSetForegroundWindow(processId);
        Interlocked.Exchange(ref activationRequested, 1);
        changes.Writer.TryWrite(true);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    public void ConfirmGraph(string graph, string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(graph);
        if (Encoding.UTF8.GetByteCount(graph) > ShaderDocument.MaximumGraphBytes)
            throw new InvalidDataException("The shader graph exceeds the 256 KiB transfer limit.");
        if (Completion.IsCompleted || Volatile.Read(ref closeRequested) != 0)
            return;
        receipts.Enqueue(new(graph, error is { Length: > 2048 } ? error[..2048] : error));
        changes.Writer.TryWrite(true);
    }

    internal static string WorkerDirectory => Path.GetDirectoryName(typeof(ShaderWorkerProcess).Assembly.Location)
        ?? throw new FileNotFoundException("The Project-Z shader editor requires its worker assemblies on disk.");

    private async Task RunAsync(string? documentPath, string? graphJson, bool hosted, bool surface, long ownerHandle, string? title, bool realtime)
    {
        string pipeName = PipeProtocol.Prefix + Guid.NewGuid().ToString("N");
        string basePath = WorkerDirectory;
        string worker = typeof(ShaderWorkerProcess).Assembly.Location;
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
        // The editor's runtimeconfig selects its own roll-forward policy.
        start.Environment.Remove("DOTNET_ROLL_FORWARD");
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(runtimeConfig);
        string? dependencies = FindDependencies(basePath, Assembly.GetEntryAssembly()?.Location);
        if (dependencies is not null)
        {
            start.ArgumentList.Add("--depsfile");
            start.ArgumentList.Add(dependencies);
        }
        start.ArgumentList.Add(worker);
        start.ArgumentList.Add(pipeName);

        using var process = new Process { StartInfo = start };
        if (!process.Start())
            throw new InvalidOperationException("The Project-Z shader editor worker could not start.");
        Volatile.Write(ref childProcessId, process.Id);
        var stderr = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExitAsync();
        var stopping = StopAfterCloseAsync(process, exited, stop.Token);
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        startup.CancelAfter(StartupTimeout);
        Task? sending = null;
        Task? receiving = null;
        try
        {
            var connected = pipe.WaitForConnectionAsync(startup.Token);
            if (await Task.WhenAny(connected, exited).ConfigureAwait(false) == exited && !connected.IsCompletedSuccessfully)
            {
                startup.Cancel();
                await IgnoreTransportEnd(connected).ConfigureAwait(false);
                if (Volatile.Read(ref closeRequested) == 0)
                    throw await WorkerExitError(process, stderr).ConfigureAwait(false);
                return;
            }
            await connected.ConfigureAwait(false);
            var settings = new ShaderWorkerStart(documentPath, Volatile.Read(ref appearance), graphJson, hosted, surface, ownerHandle, title, realtime);
            await PipeProtocol.WriteAsync(pipe, new(PipeMessage.Start, JsonSerializer.SerializeToUtf8Bytes(settings)), startup.Token)
                .ConfigureAwait(false);
            var ready = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            sending = SendChangesAsync(pipe, stop.Token);
            receiving = ReceiveResultsAsync(pipe, ready, stop.Token);
            var opening = await Task.WhenAny(ready.Task, exited, receiving, sending).WaitAsync(startup.Token).ConfigureAwait(false);
            if (!ready.Task.IsCompletedSuccessfully)
            {
                await opening.ConfigureAwait(false);
                if (Volatile.Read(ref closeRequested) == 0)
                    throw process.HasExited
                        ? await WorkerExitError(process, stderr).ConfigureAwait(false)
                        : new IOException("The shader editor stopped before its window was ready.");
                return;
            }
            Interlocked.Exchange(ref windowHandle, await ready.Task.ConfigureAwait(false));

            var first = await Task.WhenAny(exited, receiving, sending).ConfigureAwait(false);
            await first.ConfigureAwait(false);
            await exited.WaitAsync(ShutdownTimeout).ConfigureAwait(false);
            await receiving.WaitAsync(ShutdownTimeout).ConfigureAwait(false);
            if (process.ExitCode != 0 && Volatile.Read(ref closeRequested) == 0)
                throw await WorkerExitError(process, stderr).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (startup.IsCancellationRequested && Volatile.Read(ref closeRequested) == 0)
        {
            throw new TimeoutException("The Project-Z shader editor did not present a window within 15 seconds.");
        }
        catch (Exception ex) when (Volatile.Read(ref closeRequested) != 0 && ex is IOException or TimeoutException or OperationCanceledException) { }
        finally
        {
            stop.Cancel();
            Interlocked.Exchange(ref windowHandle, 0);
            Volatile.Write(ref childProcessId, 0);
            // Transport can fail without Close having been called. Always reap
            // the exact worker we started rather than leave an orphan window.
            await TerminateWorkerAsync(process, exited).ConfigureAwait(false);
            if (sending is not null)
                await IgnoreTransportEnd(sending).ConfigureAwait(false);
            await IgnoreTransportEnd(stopping).ConfigureAwait(false);
        }
    }

    private async Task StopAfterCloseAsync(Process process, Task exited, CancellationToken token)
    {
        await closeSignal.Task.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await exited.WaitAsync(ShutdownTimeout, token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await TerminateWorkerAsync(process, exited).ConfigureAwait(false);
        }
    }

    private static async Task TerminateWorkerAsync(Process process, Task exited)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited) { }
        await exited.WaitAsync(ShutdownTimeout).ConfigureAwait(false);
    }

    internal static string? FindDependencies(string directory, string? entryAssembly)
    {
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        string? entry = ComponentManifest(entryAssembly);
        if (entry is not null && IsCompatible(entry)) return entry;

        // The entry assembly can be null or located in a native host (such as a DAW).
        // Prefer an already loaded component's manifest before scanning its own directory.
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
            .Select(assembly => ComponentManifest(assembly.Location)).OfType<string>();
        return loaded.Concat(Directory.EnumerateFiles(directory, "*.deps.json").OrderBy(path => path, paths))
            .Distinct(paths).FirstOrDefault(IsCompatible);

        string? ComponentManifest(string? assembly) => string.IsNullOrEmpty(assembly) ||
            !paths.Equals(Path.GetDirectoryName(assembly), directory)
            ? null : Path.ChangeExtension(assembly, ".deps.json");

        static bool IsCompatible(string path)
        {
            try
            {
                using var file = File.OpenRead(path);
                using var document = JsonDocument.Parse(file);
                if (!document.RootElement.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object)
                    return false;
                foreach (var target in targets.EnumerateObject())
                {
                    if (target.Value.ValueKind != JsonValueKind.Object) continue;
                    bool framework = false, worker = false;
                    foreach (var library in target.Value.EnumerateObject())
                    {
                        if (library.Value.ValueKind != JsonValueKind.Object) continue;
                        foreach (string section in new[] { "runtime", "runtimeTargets" })
                        {
                            if (!library.Value.TryGetProperty(section, out var assets) || assets.ValueKind != JsonValueKind.Object)
                                continue;
                            foreach (var asset in assets.EnumerateObject())
                            {
                                string name = Path.GetFileName(asset.Name);
                                framework |= name == "ProjectZ.dll";
                                worker |= name == "ProjectZ.ShaderEditor.dll";
                            }
                        }
                        if (framework && worker) return true;
                    }
                }
                return false;
            }
            catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return false; }
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
            while (receipts.TryDequeue(out var receipt))
                await PipeProtocol.WriteAsync(pipe, new(PipeMessage.GraphReceipt, JsonSerializer.SerializeToUtf8Bytes(receipt)), token)
                    .ConfigureAwait(false);
            if (Interlocked.Exchange(ref activationRequested, 0) != 0)
                await PipeProtocol.WriteAsync(pipe, new(PipeMessage.Activate, []), token).ConfigureAwait(false);
            var settings = Encoding.UTF8.GetBytes(Volatile.Read(ref appearance));
            await PipeProtocol.WriteAsync(pipe, new(PipeMessage.Appearance, settings), token).ConfigureAwait(false);
        }
    }

    private async Task ReceiveResultsAsync(Stream pipe, TaskCompletionSource<long> ready, CancellationToken token)
    {
        while (await PipeProtocol.ReadAsync(pipe, token).ConfigureAwait(false) is { } packet)
        {
            switch (packet.Type)
            {
                case PipeMessage.Ready:
                    if (packet.Payload.Length != sizeof(long))
                        throw new InvalidDataException("Invalid shader editor window readiness.");
                    long handle = BinaryPrimitives.ReadInt64LittleEndian(packet.Payload);
                    if (handle == 0 || !ready.TrySetResult(handle))
                        throw new InvalidDataException("Unexpected shader editor window readiness.");
                    break;
                case PipeMessage.RunResult:
                    var result = PipeProtocol.ReadRunResult(packet.Payload);
                    lock (resultLock)
                    {
                        latestBytecode = result.Bytecode.Length == 0 ? null : result.Bytecode;
                        latestGraph = result.Graph;
                    }
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
        var results = Channel.CreateBounded<PipePacket>(new BoundedChannelOptions(4) { FullMode = BoundedChannelFullMode.DropOldest });
        var receipts = new ConcurrentQueue<ShaderGraphReceipt>();
        var closeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string appearance = start.Appearance;
        int closeRequested = 0;
        int activationRequested = 0;
        var reading = ReadCommands();
        var writing = WriteResults();
        var owner = WatchOwner();
        var watchdog = ExitWhenClosedHostStalls();
        Exception? failure = null;
        try
        {
            var context = AssemblyLoadContext.Default;
            context.Resolving += ResolveSibling;
            try
            {
                var framework = context.LoadFromAssemblyPath(Path.Combine(ShaderWorkerProcess.WorkerDirectory, "ProjectZ.dll"));
                string entryName = start.Realtime ? "RunRealtimeEditorWorker" : "RunOwnedEditorWorker";
                var entry = framework.GetType("ProjectZ.Shaders", throwOnError: true)!
                    .GetMethod(entryName, BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException("ProjectZ.Shaders", entryName);
                Func<string> currentAppearance = () => Volatile.Read(ref appearance);
                Func<bool> shouldClose = () => Volatile.Read(ref closeRequested) != 0;
                Func<ShaderGraphReceipt?> takeReceipt = () => receipts.TryDequeue(out var receipt) ? receipt : null;
                Func<bool> takeActivation = () => Interlocked.Exchange(ref activationRequested, 0) != 0;
                Action<byte[], string?> publish = (bytes, graph) => results.Writer.TryWrite(PipeProtocol.RunResult(bytes, graph));
                Action<long> ready = handle =>
                {
                    var payload = new byte[sizeof(long)];
                    BinaryPrimitives.WriteInt64LittleEndian(payload, handle);
                    results.Writer.TryWrite(new(PipeMessage.Ready, payload));
                };
                entry.Invoke(null, [start.DocumentPath, start.Graph, currentAppearance, publish, shouldClose, takeReceipt, start.Hosted, start.Surface,
                    start.OwnerHandle, start.Title, ready, takeActivation]);
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
            try { writing.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
            catch (TimeoutException) { }
            finally
            {
                stop.Cancel();
                Task.WhenAll(reading, writing, owner, watchdog).GetAwaiter().GetResult();
            }
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
                    if (packet.Type == PipeMessage.GraphReceipt)
                    {
                        var receipt = JsonSerializer.Deserialize<ShaderGraphReceipt>(packet.Payload) ??
                            throw new InvalidDataException("The shader editor host sent an empty graph receipt.");
                        receipts.Enqueue(receipt);
                    }
                    else if (packet.Type == PipeMessage.Appearance)
                        Volatile.Write(ref appearance, Encoding.UTF8.GetString(packet.Payload));
                    else if (packet.Type == PipeMessage.Activate && packet.Payload.Length == 0)
                        Interlocked.Exchange(ref activationRequested, 1);
                    else
                        throw new InvalidDataException("Unexpected shader editor command.");
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
            finally { RequestClose(); }
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
                RequestClose();
            }
        }

        void RequestClose()
        {
            Volatile.Write(ref closeRequested, 1);
            closeSignal.TrySetResult();
        }

        async Task WatchOwner()
        {
            if (start.OwnerHandle == 0 || !OperatingSystem.IsWindows())
                return;
            try
            {
                IntPtr handle = new(start.OwnerHandle);
                while (Volatile.Read(ref closeRequested) == 0)
                {
                    if (!IsWindow(handle))
                    {
                        RequestClose();
                        return;
                    }
                    await Task.Delay(100, stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }

        async Task ExitWhenClosedHostStalls()
        {
            try
            {
                await closeSignal.Task.WaitAsync(stop.Token).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(5), stop.Token).ConfigureAwait(false);
                // A stalled graphics call can block the form's close timer.
                // This process owns only the shader editor and its preview.
                Environment.Exit(0);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr handle);

    private static Assembly? ResolveSibling(AssemblyLoadContext context, AssemblyName name)
    {
        string path = Path.Combine(ShaderWorkerProcess.WorkerDirectory, name.Name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    }
}

public sealed record ShaderGraphReceipt(string Graph, string? Error);
internal sealed record ShaderWorkerStart(string? DocumentPath, string Appearance, string? Graph, bool Hosted, bool Surface,
    long OwnerHandle = 0, string? Title = null, bool Realtime = false);
internal enum PipeMessage : byte { Start = 1, Appearance, Close, RunResult, Failure, GraphReceipt, Ready, Activate }
internal sealed record PipePacket(PipeMessage Type, byte[] Payload);

internal static class PipeProtocol
{
    public const string Prefix = "ProjectZ.ShaderEditor.";
    private const int MaximumFrameLength = 64 * 1024 * 1024;

    public static PipePacket RunResult(byte[] bytecode, string? graph)
    {
        int graphBytes = graph is null ? 0 : Encoding.UTF8.GetByteCount(graph);
        if (graphBytes > ShaderDocument.MaximumGraphBytes || bytecode.Length > MaximumFrameLength - sizeof(int) - graphBytes)
            throw new InvalidDataException("The shader result exceeds the transfer limit.");
        var payload = new byte[sizeof(int) + graphBytes + bytecode.Length];
        BinaryPrimitives.WriteInt32LittleEndian(payload, graphBytes);
        if (graphBytes != 0)
            Encoding.UTF8.GetBytes(graph!, payload.AsSpan(sizeof(int), graphBytes));
        bytecode.CopyTo(payload, sizeof(int) + graphBytes);
        return new(PipeMessage.RunResult, payload);
    }

    public static (byte[] Bytecode, string? Graph) ReadRunResult(byte[] payload)
    {
        if (payload.Length <= sizeof(int))
            throw new InvalidDataException("The shader result is incomplete.");
        int graphBytes = BinaryPrimitives.ReadInt32LittleEndian(payload);
        if (graphBytes < 0 || graphBytes > ShaderDocument.MaximumGraphBytes || graphBytes > payload.Length - sizeof(int))
            throw new InvalidDataException("The shader result contains an invalid graph length.");
        string? graph = graphBytes == 0 ? null : Encoding.UTF8.GetString(payload, sizeof(int), graphBytes);
        return (payload[(sizeof(int) + graphBytes)..], graph);
    }

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
