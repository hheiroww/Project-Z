using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using System.Diagnostics;

using var scene = new ProbeScene();
using var loading = new SceneLoadingWork(scene);
var thread = Environment.CurrentManagedThreadId;
var workOrder = new List<int>();
var work = loading.RunAsync(new Action[]
{
    () =>
    {
        Check(Environment.CurrentManagedThreadId == thread, "Work left the scene thread.");
        workOrder.Add(1);
        Thread.Sleep(10);
    },
    () => workOrder.Add(2)
});
Check(loading.IsLoading && workOrder.Count == 0, "Starting work must return before building content.");
scene.Tick(new GameTime());
Check(workOrder.SequenceEqual(new[] { 1 }) && !work.IsCompleted, "A slow step must yield before the next step.");
Drain(work);
Check(workOrder.SequenceEqual(new[] { 1, 2 }) && work.IsCompletedSuccessfully && !loading.IsLoading,
    "The scene must resume and complete the remaining work in order.");

var stale = loading.RunAsync(new Action[] { () => throw new Exception("Stale content was built.") });
using var element = new ProbeElement();
var latest = loading.RunAsync(new Action[] { () => scene.AddElement(element) });
Check(stale.IsCanceled, "A newer request must cancel its predecessor.");
Drain(latest);
scene.Tick(new GameTime());
Check(latest.IsCompletedSuccessfully && scene.ContainsElement(element), "Queued work must register scene elements.");
scene.RemoveElement(element);
scene.AddElement(element);
scene.Tick(new GameTime());
Check(scene.ContainsElement(element), "Reattaching content must cancel its queued removal.");

using var tree = new ProbeElement();
var children = Enumerable.Range(0, 10_000).Select(_ => new ProbeElement()).ToArray();
tree.Children.AddRange(children);
children[20].zIndex = 1;
children[4].IsOverlay = true;
var graphStarted = Stopwatch.GetTimestamp();
scene.AddElement(tree);
scene.Tick(new GameTime());
var registrationTime = Stopwatch.GetElapsedTime(graphStarted);
Check(scene.ContainsElement(children[^1]), "The scene must register every child of a large panel.");
graphStarted = Stopwatch.GetTimestamp();
var first = scene.GetRenderOrderIndex(children[0]);
var last = scene.GetRenderOrderIndex(children[^1]);
var raised = scene.GetRenderOrderIndex(children[20]);
var overlay = scene.GetRenderOrderIndex(children[4]);
var sortTime = Stopwatch.GetElapsedTime(graphStarted);
Check(first < last && last < raised && raised < overlay, "Sibling, z-index, and overlay sort precedence must be preserved.");
Console.WriteLine($"10,000 sibling controls: registration {registrationTime.TotalMilliseconds:F1} ms, render ordering {sortTime.TotalMilliseconds:F1} ms.");

Task? replacement = null;
var superseded = loading.RunAsync(new Action[]
{
    () => replacement = loading.RunAsync(new Action[] { () => workOrder.Add(3) }),
    () => throw new Exception("A superseded request continued building.")
});
scene.Tick(new GameTime());
Check(superseded.IsCanceled && replacement is not null && !replacement.IsCompleted,
    "Replacing a request from a step must defer its successor to another frame.");
Drain(replacement!);
Check(workOrder.SequenceEqual(new[] { 1, 2, 3 }), "The replacement must run on a subsequent frame.");

using var cancellation = new CancellationTokenSource();
var cancelled = loading.RunAsync(new Action[] { () => throw new Exception("Cancelled content was built.") }, cancellation.Token);
cancellation.Cancel();
scene.Tick(new GameTime());
Check(cancelled.IsCanceled && !loading.IsLoading, "Caller cancellation must finish without running a step.");

var failed = loading.RunAsync(new Action[] { () => throw new InvalidOperationException("expected failure") });
Drain(failed);
Check(failed.Exception?.InnerException?.Message == "expected failure" && !loading.IsLoading,
    "A step failure must reach the caller and clear loading state.");

var pending = loading.RunAsync(new Action[] { () => throw new Exception("Disposed scene built content.") });
scene.Dispose();
Check(pending.IsCanceled && !loading.IsLoading, "Disposing the scene must cancel pending loading.");
Console.WriteLine("PASS scene loading budget, registration, supersession, cancellation, failure, and disposal");

void Drain(Task task)
{
    for (var frame = 0; frame < 20 && !task.IsCompleted; frame++)
        scene.Tick(new GameTime());
    Check(task.IsCompleted, "Loading did not complete after twenty frames.");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

sealed class ProbeScene : Scene
{
    public override void Initialize(GameTime gameTime) { }
}

sealed class ProbeElement : SceneElement
{
    protected override void Draw(GameTime gameTime) { }
}
