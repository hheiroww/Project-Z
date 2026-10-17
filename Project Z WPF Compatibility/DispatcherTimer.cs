using System.Diagnostics;

namespace ProjectZ.WpfCompatibility.Threading;

/// <summary>Scene-driven timers do not allocate a polling thread or build up missed ticks.</summary>
public sealed class DispatcherTimer
{
    readonly DispatcherPriority priority;
    TimeSpan interval;
    long due;
    bool enabled;
    DispatcherOperation? pending;
    public Dispatcher Dispatcher { get; }
    public object? Tag { get; set; }
    public event EventHandler? Tick;
    public bool IsEnabled { get => enabled; set { if (value) Start(); else Stop(); } }
    public TimeSpan Interval
    {
        get => interval;
        set
        {
            Dispatcher.VerifyAccess();
            if (value < TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            interval = value; if (IsEnabled) { pending?.Abort(); pending = null; RestartDeadline(); }
        }
    }
    public DispatcherTimer() : this(DispatcherPriority.Background) { }
    public DispatcherTimer(DispatcherPriority priority) : this(priority, Dispatcher.CurrentDispatcher) { }
    public DispatcherTimer(DispatcherPriority priority, Dispatcher dispatcher)
    {
        Dispatcher.ValidatePriority(priority); Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)); this.priority = priority;
    }
    public DispatcherTimer(TimeSpan interval, DispatcherPriority priority, EventHandler callback, Dispatcher dispatcher) : this(priority, dispatcher)
    {
        ArgumentNullException.ThrowIfNull(callback); Interval = interval; Tick += callback; Start();
    }
    void RestartDeadline() => due = Stopwatch.GetTimestamp() + (long)(interval.TotalSeconds * Stopwatch.Frequency);
    public void Start()
    {
        Dispatcher.VerifyAccess(); if (IsEnabled) return;
        Dispatcher.AddTimer(this); enabled = true; RestartDeadline();
    }
    public void Stop()
    {
        Dispatcher.VerifyAccess(); enabled = false; Dispatcher.RemoveTimer(this); pending?.Abort(); pending = null;
    }
    internal void ScheduleTick()
    {
        if (!IsEnabled || pending != null || Stopwatch.GetTimestamp() < due) return;
        pending = Dispatcher.BeginInvoke(() =>
        {
            pending = null; if (!IsEnabled) return;
            try { Tick?.Invoke(this, EventArgs.Empty); }
            finally { if (IsEnabled) RestartDeadline(); }
        }, priority);
    }
}
