using System.Collections.Concurrent;

namespace ProjectZ.WpfCompatibility.Threading;

internal sealed class DispatcherSynchronizationContext(Dispatcher dispatcher) : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) => _ = dispatcher.InvokeAsync(() => callback(state));
    public override void Send(SendOrPostCallback callback, object? state) => dispatcher.Invoke(() => callback(state));
    public override SynchronizationContext CreateCopy() => new DispatcherSynchronizationContext(dispatcher);
}

public sealed class Dispatcher
{
    readonly int thread = Environment.CurrentManagedThreadId;
    readonly ConcurrentQueue<Action> queue = new();
    public bool CheckAccess() => Environment.CurrentManagedThreadId == thread;
    public void VerifyAccess() { if (!CheckAccess()) throw new InvalidOperationException("Project-Z UI access requires its scene thread."); }
    public Task InvokeAsync(Action callback)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Enqueue(() => { try { callback(); completion.SetResult(); } catch (Exception e) { completion.SetException(e); } });
        return completion.Task;
    }
    public Task BeginInvoke(Action callback) => InvokeAsync(callback);
    public void Invoke(Action callback) { if (CheckAccess()) callback(); else InvokeAsync(callback).GetAwaiter().GetResult(); }
    public void Pump() { VerifyAccess(); while (queue.TryDequeue(out var action)) action(); }
}
