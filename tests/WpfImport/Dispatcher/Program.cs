using ProjectZ.WpfCompatibility.Threading;
using System.Runtime.CompilerServices;

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine("PASS " + description);
}
static void Drain(Dispatcher dispatcher, Func<bool> complete)
{
    var timeout = DateTime.UtcNow.AddSeconds(5);
    while (!complete() && DateTime.UtcNow < timeout) { dispatcher.Pump(); Thread.Yield(); }
    Require(complete(), "dispatcher completes work within deadline");
}
[MethodImpl(MethodImplOptions.NoInlining)]
static (DispatcherOperation Operation, WeakReference Payload) RetainedOperation(Dispatcher dispatcher, bool abort)
{
    var payload = new byte[1024 * 1024];
    var weak = new WeakReference(payload);
    var operation = dispatcher.InvokeAsync(() => { GC.KeepAlive(payload); }, abort ? DispatcherPriority.Inactive : DispatcherPriority.Normal);
    if (abort) operation.Abort(); else dispatcher.Pump();
    return (operation, weak);
}
var dispatcher = Dispatcher.CurrentDispatcher;
var order = new List<int>();
dispatcher.InvokeAsync(() => { order.Add(4); }, DispatcherPriority.Background);
dispatcher.InvokeAsync(() => { order.Add(2); }, DispatcherPriority.Normal);
dispatcher.InvokeAsync(() => { order.Add(3); }, DispatcherPriority.Normal);
dispatcher.InvokeAsync(() => { order.Add(1); }, DispatcherPriority.Send);
Drain(dispatcher, () => order.Count == 4);
Require(order.SequenceEqual(new[] { 1, 2, 3, 4 }), "priority ordering preserves FIFO within a priority");
var inactive = dispatcher.InvokeAsync(() => throw new Exception("inactive ran"), DispatcherPriority.Inactive);
dispatcher.Pump(); Require(inactive.Status == DispatcherOperationStatus.Pending, "inactive work is not executed");
Require(inactive.Abort() && inactive.Task.IsCanceled, "inactive cancellation removes queued operation");
var promoted = dispatcher.InvokeAsync(() => { order.Add(5); }, DispatcherPriority.Inactive);
promoted.Priority = DispatcherPriority.Send; dispatcher.Pump();
Require(promoted.Status == DispatcherOperationStatus.Completed && order.Last() == 5, "priority changes move pending work between queues");
var result = dispatcher.InvokeAsync(() => 42);
Drain(dispatcher, () => result.Task.IsCompleted);
Require(result.Task.Result == 42, "generic dispatcher operation returns its result");
using (var cancellation = new CancellationTokenSource())
{
    var canceled = dispatcher.InvokeAsync(() => throw new Exception("canceled ran"), DispatcherPriority.Normal, cancellation.Token);
    cancellation.Cancel(); dispatcher.Pump();
    Require(canceled.Status == DispatcherOperationStatus.Aborted && canceled.Task.IsCanceled, "cancellation before dispatch prevents execution");
}
using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel(); var operation = dispatcher.InvokeAsync(() => { }, cancellationToken: canceled.Token);
    dispatcher.Pump(); Require(operation.Task.IsCanceled, "already canceled token is not enqueued");
}
var fault = dispatcher.InvokeAsync(() => { throw new InvalidOperationException("expected"); }); dispatcher.Pump();
Require(fault.Task.IsFaulted && fault.Task.Exception!.InnerException!.Message == "expected", "callback failure reaches awaitable task");
int reported = 0;
DispatcherUnhandledExceptionEventHandler handler = (_, args) => { reported++; args.Handled = true; };
dispatcher.UnhandledException += handler;
var postedFault = dispatcher.BeginInvoke(() => { throw new InvalidOperationException("posted"); }); dispatcher.Pump();
Require(reported == 1 && postedFault.Task.IsFaulted, "BeginInvoke faults reach the dispatcher exception handler");
dispatcher.UnhandledException -= handler;
dispatcher.BeginInvoke(() => { throw new InvalidOperationException("unhandled"); });
try { dispatcher.Pump(); throw new Exception("unhandled error swallowed"); }
catch (InvalidOperationException error) { Require(error.Message == "unhandled", "unhandled posted callback propagates to host"); }
int recursion = 0; Action? repeat = null;
repeat = () => { recursion++; if (recursion < 10) dispatcher.InvokeAsync(repeat!); };
dispatcher.InvokeAsync(repeat); dispatcher.Pump();
Require(recursion == 1, "self scheduling work yields to the next frame");
Drain(dispatcher, () => recursion == 10);
int sceneThread = Environment.CurrentManagedThreadId, workerResult = 0;
var worker = Task.Run(() => { Require(!dispatcher.CheckAccess(), "worker cannot access scene UI directly"); workerResult = dispatcher.Invoke(() => Environment.CurrentManagedThreadId); });
Drain(dispatcher, () => worker.IsCompleted);
Require(worker.IsCompletedSuccessfully && workerResult == sceneThread, "synchronous worker Invoke executes on scene thread");
int ticks = 0;
var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.Zero };
timer.Tick += (_, _) => ticks++; timer.Start(); dispatcher.Pump();
Require(ticks == 1, "zero interval timer ticks once per frame");
dispatcher.Pump(); Require(ticks == 2, "timer schedules subsequent frame");
dispatcher.InvokeAsync(timer.Stop, DispatcherPriority.Send); dispatcher.Pump();
Require(ticks == 2 && !timer.IsEnabled, "stop cancels a tick already queued at lower priority");
timer.Interval = TimeSpan.FromHours(1); timer.Start(); dispatcher.Pump();
Require(ticks == 2, "timer respects interval without polling ticks");
timer.Interval = TimeSpan.Zero; dispatcher.Pump(); Require(ticks == 3, "interval change reschedules deadline");
int backlog = 0;
for (int i = 0; i < 10000; i++) dispatcher.InvokeAsync(() => { backlog++; });
dispatcher.Pump(); Require(backlog is > 0 and <= 1024, "large queue is bounded per frame");
Drain(dispatcher, () => backlog == 10000);
var shutdownWork = dispatcher.InvokeAsync(() => throw new Exception("shutdown ran"));
dispatcher.InvokeShutdown(); dispatcher.Pump();
Require(shutdownWork.Task.IsCanceled && !timer.IsEnabled && dispatcher.HasShutdownFinished, "shutdown aborts work and releases timers");
Require(dispatcher.InvokeAsync(() => { }).Task.IsCanceled, "shutdown dispatcher rejects new work");
var cleanupDispatcher = Dispatcher.CurrentDispatcher;
var abortedPayload = RetainedOperation(cleanupDispatcher, true);
var completedPayload = RetainedOperation(cleanupDispatcher, false);
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
Require(!abortedPayload.Payload.IsAlive && !completedPayload.Payload.IsAlive, "retained completed and aborted operations release callback payloads");
GC.KeepAlive(abortedPayload.Operation); GC.KeepAlive(completedPayload.Operation); cleanupDispatcher.InvokeShutdown();
