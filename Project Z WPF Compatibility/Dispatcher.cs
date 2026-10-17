using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace ProjectZ.WpfCompatibility.Threading;

public enum DispatcherPriority { Invalid = -1, Inactive, SystemIdle, ApplicationIdle, ContextIdle, Background, Input, Loaded, Render, DataBind, Normal, Send }
public enum DispatcherOperationStatus { Pending, Aborted, Completed, Executing }
public sealed class DispatcherUnhandledExceptionEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
    public bool Handled { get; set; }
}
public delegate void DispatcherUnhandledExceptionEventHandler(object sender, DispatcherUnhandledExceptionEventArgs e);

internal sealed class DispatcherSynchronizationContext(Dispatcher dispatcher) : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) => _ = dispatcher.BeginInvoke(() => callback(state));
    public override void Send(SendOrPostCallback callback, object? state) => dispatcher.Invoke(() => callback(state));
    public override SynchronizationContext CreateCopy() => new DispatcherSynchronizationContext(dispatcher);
}

public class DispatcherOperation
{
    Func<object?>? callback;
    readonly TaskCompletionSource<object?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationTokenRegistration cancellation;
    int status;
    int priority;
    readonly bool reportUnhandled;
    internal long Sequence { get; }
    public Dispatcher Dispatcher { get; }
    public Task Task => completion.Task;
    public DispatcherOperationStatus Status => (DispatcherOperationStatus)Volatile.Read(ref status);
    public event EventHandler? Completed, Aborted;
    public DispatcherPriority Priority { get => (DispatcherPriority)Volatile.Read(ref priority); set { Dispatcher.ValidatePriority(value); Dispatcher.Reprioritize(this, value); } }
    internal void SetPriority(DispatcherPriority value) => Volatile.Write(ref priority, (int)value);
    internal Task<object?> ResultTask => completion.Task;
    internal DispatcherOperation(Dispatcher dispatcher, Func<object?> callback, DispatcherPriority priority, long sequence, CancellationToken token, bool reportUnhandled = false)
    {
        Dispatcher = dispatcher; this.callback = callback; this.priority = (int)priority; Sequence = sequence;
        this.reportUnhandled = reportUnhandled;
        if (token.CanBeCanceled) cancellation = token.Register(() => Abort());
    }
    public TaskAwaiter GetAwaiter() => Task.GetAwaiter();
    public bool Abort()
    {
        if (Interlocked.CompareExchange(ref status, (int)DispatcherOperationStatus.Aborted, (int)DispatcherOperationStatus.Pending) != (int)DispatcherOperationStatus.Pending) return false;
        Dispatcher.RemovePending(this); Interlocked.Exchange(ref callback, null); completion.TrySetCanceled(); cancellation.Unregister(); Aborted?.Invoke(this, EventArgs.Empty); return true;
    }
    internal void Execute()
    {
        if (Interlocked.CompareExchange(ref status, (int)DispatcherOperationStatus.Executing, (int)DispatcherOperationStatus.Pending) != (int)DispatcherOperationStatus.Pending) return;
        object? result = null; Exception? failure = null;
        var invoke = Interlocked.Exchange(ref callback, null);
        try { result = invoke!(); }
        catch (Exception error) { failure = error is TargetInvocationException { InnerException: not null } invocation ? invocation.InnerException : error; }
        Volatile.Write(ref status, (int)DispatcherOperationStatus.Completed); cancellation.Unregister();
        if (failure == null) completion.TrySetResult(result); else completion.TrySetException(failure);
        Completed?.Invoke(this, EventArgs.Empty);
        if (failure != null && reportUnhandled) Dispatcher.ReportUnhandledException(failure);
    }
}
public sealed class DispatcherOperation<T> : DispatcherOperation
{
    public new Task<T> Task { get; }
    internal DispatcherOperation(Dispatcher dispatcher, Func<T> callback, DispatcherPriority priority, long sequence, CancellationToken token)
        : base(dispatcher, () => callback(), priority, sequence, token) => Task = Result();
    async Task<T> Result() => (T)(await ResultTask.ConfigureAwait(false))!;
    public new TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();
}

public sealed class Dispatcher
{
    [ThreadStatic] static Dispatcher? current;
    public static Dispatcher CurrentDispatcher => current ??= new Dispatcher();
    readonly int thread = Environment.CurrentManagedThreadId;
    readonly object gate = new();
    readonly SortedDictionary<long, DispatcherOperation>[] queues = Enumerable.Range(0, 11).Select(_ => new SortedDictionary<long, DispatcherOperation>()).ToArray();
    readonly HashSet<DispatcherTimer> timers = new();
    long sequence;
    bool pumping;
    public bool HasShutdownStarted { get; private set; }
    public bool HasShutdownFinished { get; private set; }
    public event DispatcherUnhandledExceptionEventHandler? UnhandledException;
    public Dispatcher() { current ??= this; }
    public bool CheckAccess() => Environment.CurrentManagedThreadId == thread;
    public void VerifyAccess() { if (!CheckAccess()) throw new InvalidOperationException("Project-Z UI access requires its scene thread."); }
    internal static void ValidatePriority(DispatcherPriority priority)
    {
        if (priority < DispatcherPriority.Inactive || priority > DispatcherPriority.Send) throw new ArgumentOutOfRangeException(nameof(priority));
    }
    void Enqueue(DispatcherOperation operation)
    {
        bool shutdown;
        lock (gate) { shutdown = HasShutdownStarted; if (!shutdown && operation.Status == DispatcherOperationStatus.Pending) queues[(int)operation.Priority].Add(operation.Sequence, operation); }
        if (shutdown) operation.Abort();
    }
    internal void RemovePending(DispatcherOperation operation) { lock (gate) queues[(int)operation.Priority].Remove(operation.Sequence); }
    internal void Reprioritize(DispatcherOperation operation, DispatcherPriority value)
    {
        lock (gate)
        {
            bool queued = queues[(int)operation.Priority].Remove(operation.Sequence); operation.SetPriority(value);
            if (queued) queues[(int)value].Add(operation.Sequence, operation);
        }
    }
    public DispatcherOperation InvokeAsync(Action callback, DispatcherPriority priority = DispatcherPriority.Normal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback); ValidatePriority(priority);
        var operation = new DispatcherOperation(this, () => { callback(); return null; }, priority, Interlocked.Increment(ref sequence), cancellationToken);
        Enqueue(operation); return operation;
    }
    public DispatcherOperation<T> InvokeAsync<T>(Func<T> callback, DispatcherPriority priority = DispatcherPriority.Normal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback); ValidatePriority(priority);
        var operation = new DispatcherOperation<T>(this, callback, priority, Interlocked.Increment(ref sequence), cancellationToken);
        Enqueue(operation); return operation;
    }
    public DispatcherOperation BeginInvoke(Action callback, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(callback); ValidatePriority(priority);
        var operation = new DispatcherOperation(this, () => { callback(); return null; }, priority, Interlocked.Increment(ref sequence), default, true);
        Enqueue(operation); return operation;
    }
    public DispatcherOperation BeginInvoke(DispatcherPriority priority, Delegate callback, params object?[] args) => BeginInvoke(callback, priority, args);
    public DispatcherOperation BeginInvoke(Delegate callback, DispatcherPriority priority, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(callback); return BeginInvoke(() => { callback.DynamicInvoke(args); }, priority);
    }
    public void Invoke(Action callback, DispatcherPriority priority = DispatcherPriority.Send)
    {
        ArgumentNullException.ThrowIfNull(callback); ValidatePriority(priority);
        if (CheckAccess()) callback(); else InvokeAsync(callback, priority).Task.GetAwaiter().GetResult();
    }
    public T Invoke<T>(Func<T> callback, DispatcherPriority priority = DispatcherPriority.Send)
    {
        ArgumentNullException.ThrowIfNull(callback); ValidatePriority(priority);
        return CheckAccess() ? callback() : InvokeAsync(callback, priority).Task.GetAwaiter().GetResult();
    }
    public static Task Yield(DispatcherPriority priority = DispatcherPriority.Background) => CurrentDispatcher.InvokeAsync(() => { }, priority).Task;
    internal void ReportUnhandledException(Exception exception)
    {
        var args = new DispatcherUnhandledExceptionEventArgs(exception); UnhandledException?.Invoke(this, args);
        if (!args.Handled) ExceptionDispatchInfo.Capture(exception).Throw();
    }
    internal void AddTimer(DispatcherTimer timer) { lock (gate) { if (HasShutdownStarted) throw new InvalidOperationException("The dispatcher has shut down."); timers.Add(timer); } }
    internal void RemoveTimer(DispatcherTimer timer) { lock (gate) timers.Remove(timer); }
    public void InvokeShutdown()
    {
        VerifyAccess(); DispatcherOperation[] pending; DispatcherTimer[] active;
        lock (gate) { HasShutdownStarted = true; pending = queues.SelectMany(q => q.Values).ToArray(); foreach (var queue in queues) queue.Clear(); active = timers.ToArray(); timers.Clear(); }
        foreach (var timer in active) timer.Stop(); foreach (var operation in pending) operation.Abort(); HasShutdownFinished = true;
        if (current == this) current = null;
    }
    public void Pump()
    {
        VerifyAccess(); if (pumping || HasShutdownStarted) return;
        pumping = true;
        try
        {
            DispatcherTimer[] due; lock (gate) due = timers.ToArray();
            foreach (var timer in due) timer.ScheduleTick();
            long cutoff = Volatile.Read(ref sequence), start = Stopwatch.GetTimestamp();
            for (int executed = 0; executed < 1024; executed++)
            {
                DispatcherOperation? next = null;
                lock (gate)
                {
                    for (int priority = (int)DispatcherPriority.Send; priority > (int)DispatcherPriority.Inactive; priority--)
                    {
                        var queue = queues[priority]; if (queue.Count == 0) continue;
                        var first = queue.First(); if (first.Key > cutoff) continue;
                        next = first.Value; queue.Remove(first.Key); break;
                    }
                }
                if (next == null) break; next.Execute();
                if (Stopwatch.GetElapsedTime(start).TotalMilliseconds >= 8) break;
            }
        }
        finally { pumping = false; }
    }
}
