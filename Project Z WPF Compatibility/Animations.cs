using System.Collections.ObjectModel;
using System.Reflection;
using ProjectZ.Shared.Animations;
using NativeEase = ProjectZ.Shared.Animations.Easing;

namespace ProjectZ.WpfCompatibility
{
    public readonly struct Duration
    {
        readonly TimeSpan? value;
        public Duration(TimeSpan timeSpan) => value = timeSpan;
        public bool HasTimeSpan => value.HasValue;
        public TimeSpan TimeSpan => value ?? System.TimeSpan.FromSeconds(1);
        public static Duration Automatic => default;
        public static Duration Forever => new(System.TimeSpan.MaxValue);
        public static implicit operator Duration(TimeSpan value) => new(value);
    }
    public class Freezable : DependencyObject
    {
        public bool IsFrozen { get; private set; }
        public bool CanFreeze => true;
        public virtual void Freeze() => IsFrozen = true;
    }
}
namespace ProjectZ.WpfCompatibility.Media.Animation
{
    public enum HandoffBehavior { SnapshotAndReplace, Compose }
    public enum FillBehavior { HoldEnd, Stop }
    public enum EasingMode { EaseOut, EaseIn, EaseInOut }
    public readonly struct RepeatBehavior
    {
        public double Count { get; }
        public TimeSpan Duration { get; }
        public bool HasDuration { get; }
        public RepeatBehavior(double count) { if (count < 0 || double.IsNaN(count)) throw new ArgumentOutOfRangeException(nameof(count)); Count = count; }
        public RepeatBehavior(TimeSpan duration) { Duration = duration; HasDuration = true; }
        public static RepeatBehavior Forever => new(double.PositiveInfinity);
    }
    public interface IEasingFunction { double Ease(double normalizedTime); }
    public abstract class EasingFunctionBase : Freezable, IEasingFunction
    {
        public EasingMode EasingMode { get; set; } = EasingMode.EaseOut;
        protected NativeEase.EaseType NativeMode => EasingMode switch { EasingMode.EaseIn => NativeEase.EaseType.EaseIn, EasingMode.EaseInOut => NativeEase.EaseType.EaseInOut, _ => NativeEase.EaseType.EaseOut };
        public abstract double Ease(double normalizedTime);
    }
    public class SineEase : EasingFunctionBase { public override double Ease(double time) => new NativeEase.SineEase(NativeMode).Ease(time); }
    public class PowerEase : EasingFunctionBase
    {
        public double Power { get; set; } = 2;
        public override double Ease(double t) => EasingMode switch { EasingMode.EaseIn => Math.Pow(t, Power), EasingMode.EaseOut => 1 - Math.Pow(1 - t, Power), _ => t < .5 ? Math.Pow(t * 2, Power) / 2 : 1 - Math.Pow((1 - t) * 2, Power) / 2 };
    }
    public class CubicEase : PowerEase { public CubicEase() => Power = 3; }
    public class QuadraticEase : PowerEase { }
    public class Animatable : Freezable { }
    public abstract class Timeline : Freezable
    {
        internal DependencyObject? Target;
        internal PropertyPath? TargetProperty;
        internal string? TargetName;
        public Duration Duration { get; set; } = Duration.Automatic;
        public TimeSpan? BeginTime { get; set; } = TimeSpan.Zero;
        public RepeatBehavior RepeatBehavior { get; set; } = new(1);
        public bool AutoReverse { get; set; }
        public double SpeedRatio { get; set; } = 1;
        public FillBehavior FillBehavior { get; set; }
        public event EventHandler? Completed;
        internal void Complete() => Completed?.Invoke(this, EventArgs.Empty);
    }
    public abstract class AnimationTimeline : Timeline
    {
        internal abstract object Interpolate(object initial, double progress);
    }
    public class DoubleAnimation : AnimationTimeline
    {
        public double? From { get; set; } public double? To { get; set; } public double? By { get; set; }
        public IEasingFunction? EasingFunction { get; set; }
        public DoubleAnimation() { }
        public DoubleAnimation(double toValue, Duration duration) { To = toValue; Duration = duration; }
        public DoubleAnimation(double fromValue, double toValue, Duration duration) : this(toValue, duration) => From = fromValue;
        internal override object Interpolate(object initial, double progress)
        {
            var first = From ?? Convert.ToDouble(initial); var last = To ?? first + (By ?? 0);
            return global::ProjectZ.Shared.Animations.DoubleAnimation.Interpolate(EasingFunction?.Ease(progress) ?? progress, 0, 1, first, last);
        }
    }
    public class ThicknessAnimation : AnimationTimeline
    {
        public Thickness? From { get; set; } public Thickness? To { get; set; } public Thickness? By { get; set; }
        public IEasingFunction? EasingFunction { get; set; }
        public ThicknessAnimation() { }
        public ThicknessAnimation(Thickness toValue, Duration duration) { To = toValue; Duration = duration; }
        public ThicknessAnimation(Thickness fromValue, Thickness toValue, Duration duration) : this(toValue, duration) => From = fromValue;
        internal override object Interpolate(object initial, double progress)
        {
            var a = From ?? (Thickness)initial; var b = To ?? a; var p = EasingFunction?.Ease(progress) ?? progress;
            return new Thickness(Lerp(a.Left,b.Left,p), Lerp(a.Top,b.Top,p), Lerp(a.Right,b.Right,p), Lerp(a.Bottom,b.Bottom,p));
        }
        static double Lerp(double a, double b, double p) => global::ProjectZ.Shared.Animations.DoubleAnimation.Interpolate(p, 0, 1, a, b);
    }
    public class ColorAnimation : AnimationTimeline
    {
        public Color? From { get; set; } public Color? To { get; set; }
        public IEasingFunction? EasingFunction { get; set; }
        internal override object Interpolate(object initial, double p) => Lerp(From ?? (Color)initial, To ?? (Color)initial, EasingFunction?.Ease(p) ?? p);
        internal static Color Lerp(Color a, Color b, double p) => Color.FromArgb(C(a.A,b.A,p), C(a.R,b.R,p), C(a.G,b.G,p), C(a.B,b.B,p));
        static byte C(byte a, byte b, double p) => (byte)Math.Clamp(Math.Round(a+(b-a)*p),0,255);
    }
    public readonly struct KeyTime
    {
        public TimeSpan TimeSpan { get; }
        public double? Percent { get; }
        public KeyTime(TimeSpan value) => TimeSpan = value;
        KeyTime(double percent)=>Percent=percent;
        public static KeyTime FromPercent(double percent) => percent>=0 && percent<=1 ? new(percent) : throw new ArgumentOutOfRangeException(nameof(percent));
        internal double Seconds(double length) => Percent is { } percent ? percent*length : TimeSpan.TotalSeconds;
        public static KeyTime FromTimeSpan(TimeSpan value) => new(value);
        public static implicit operator KeyTime(TimeSpan value) => new(value);
    }
    public class LinearColorKeyFrame
    {
        public Color Value { get; set; } public KeyTime KeyTime { get; set; }
        public LinearColorKeyFrame() { }
        public LinearColorKeyFrame(Color value, KeyTime keyTime) { Value=value; KeyTime=keyTime; }
    }
    public class ColorKeyFrameCollection : Collection<LinearColorKeyFrame> { }
    public class ColorAnimationUsingKeyFrames : AnimationTimeline
    {
        public ColorKeyFrameCollection KeyFrames { get; } = new();
        internal override object Interpolate(object initial, double progress)
        {
            var previous = (Color)initial; var previousTime = 0d;
            var length = Duration.HasTimeSpan ? Duration.TimeSpan.TotalSeconds : KeyFrames.Select(k => k.KeyTime.TimeSpan.TotalSeconds).DefaultIfEmpty(1).Max();
            var elapsed = progress * length;
            foreach (var frame in KeyFrames.OrderBy(k => k.KeyTime.Seconds(length)))
            {
                var end = frame.KeyTime.Seconds(length);
                if (elapsed <= end) return ColorAnimation.Lerp(previous, frame.Value, end <= previousTime ? 1 : (elapsed - previousTime)/(end-previousTime));
                previous = frame.Value; previousTime=end;
            }
            return previous;
        }
    }
    public class TimelineCollection : Collection<Timeline> { }
    public abstract class TimelineGroup : Timeline { public TimelineCollection Children { get; } = new(); }
    public class Storyboard : TimelineGroup
    {
        readonly List<PropertyAnimationClock> clocks = new();
        public static void SetTarget(Timeline timeline, DependencyObject target) => timeline.Target = target;
        public static void SetTargetProperty(Timeline timeline, PropertyPath property) => timeline.TargetProperty = property;
        public static void SetTargetName(Timeline timeline, string name) => timeline.TargetName = name;
        public void Begin() => Begin(null, HandoffBehavior.SnapshotAndReplace, false);
        public void Begin(FrameworkElement containingObject) => Begin(containingObject, HandoffBehavior.SnapshotAndReplace, false);
        public void Begin(FrameworkElement? containingObject, bool isControllable) => Begin(containingObject, HandoffBehavior.SnapshotAndReplace, isControllable);
        public void Begin(FrameworkElement? containingObject, HandoffBehavior handoffBehavior, bool isControllable)
        {
            Stop(); var pending = 0;
            if(BeginTime==null) return;
            var active=Children.OfType<AnimationTimeline>().Where(a=>a.BeginTime!=null).ToArray();
            if(SpeedRatio<=0 || active.Any(a=>a.SpeedRatio<=0)) throw new ArgumentOutOfRangeException(nameof(SpeedRatio));
            var naturalDuration=active.Select(a=>a.BeginTime!.Value.TotalSeconds+(a.RepeatBehavior.HasDuration?a.RepeatBehavior.Duration.TotalSeconds:a.Duration.TimeSpan.TotalSeconds*(a.AutoReverse?2:1)*a.RepeatBehavior.Count)/a.SpeedRatio).DefaultIfEmpty(0).Max();
            var groupDuration=Duration.HasTimeSpan ? Duration.TimeSpan : double.IsPositiveInfinity(naturalDuration)?TimeSpan.MaxValue:TimeSpan.FromSeconds(naturalDuration);
            foreach (var child in Children)
            {
                if (child is not AnimationTimeline animation) throw new NotSupportedException("Nested storyboard clocks require an animation timeline.");
                var target = child.Target ?? (child.TargetName != null ? containingObject?.FindName(child.TargetName) as DependencyObject : containingObject) ?? throw new InvalidOperationException("Storyboard target is missing.");
                var path = child.TargetProperty ?? throw new InvalidOperationException("Storyboard target property is missing.");
                var clock = AnimationHost.Start(target, path, animation, handoffBehavior,this,groupDuration);
                if (clock == null) continue;
                clocks.Add(clock); pending++;
                clock.Completed += (_, _) => { if (--pending == 0) Complete(); };
            }
        }
        public void Stop() { foreach (var clock in clocks) clock.Stop(); clocks.Clear(); }
        public void Stop(FrameworkElement owner) => Stop();
        public void Remove() => Stop(); public void Remove(FrameworkElement owner) => Stop();
        public void Pause() { foreach (var clock in clocks) clock.IsPaused=true; }
        public void Pause(FrameworkElement owner) => Pause();
        public void Resume() { foreach (var clock in clocks) clock.IsPaused=false; }
        public void Resume(FrameworkElement owner) => Resume();
    }
    internal static class AnimationHost
    {
        public static PropertyAnimationClock? Start(DependencyObject target, PropertyPath path, AnimationTimeline? animation, HandoffBehavior handoff,TimelineGroup? group=null,TimeSpan? groupDuration=null)
        {
            var name = path.Path.Trim('(', ')').Split('.').Last();
            var member = target.GetType().GetProperty(name) ?? throw new MissingMemberException(target.GetType().Name, name);
            var initial = member.GetValue(target)!;
            if (handoff == HandoffBehavior.SnapshotAndReplace) target.RemoveAnimation(name);
            if (animation == null || animation.BeginTime == null) return null;
            var scene = target is UIElement element ? element.NativeElement.OwnerScene : target.AnimationScene ?? Application.Current.ControlScene;
            if(animation.SpeedRatio<=0 || group?.SpeedRatio<=0) throw new ArgumentOutOfRangeException(nameof(animation.SpeedRatio));
            var clockTimeline=(Timeline?)group??animation;
            var clock = new PropertyAnimationClock(scene, p =>
            {
                if (target is UIElement { IsDisposed: true }) { target.RemoveAnimation(name); return; }
                if(group!=null)
                {
                    var elapsed=(p*groupDuration!.Value.TotalSeconds-animation.BeginTime.Value.TotalSeconds)*animation.SpeedRatio;
                    if(elapsed<0) {member.SetValue(target,initial);return;}
                    var length=animation.Duration.TimeSpan.TotalSeconds;var cycle=length*(animation.AutoReverse?2:1);
                    var limit=animation.RepeatBehavior.HasDuration?animation.RepeatBehavior.Duration.TotalSeconds:cycle*animation.RepeatBehavior.Count;
                    var finished=length<=0||elapsed>=limit;
                    if(finished && animation.FillBehavior==FillBehavior.Stop) {member.SetValue(target,initial);return;}
                    var phase=cycle<=0?0:Math.Min(elapsed,limit)%cycle;
                    if(finished && phase==0) phase=cycle;
                    p=length<=0?1:phase/length;
                    if(animation.AutoReverse && p>1) p=2-p;
                }
                member.SetValue(target, animation.Interpolate(initial,Math.Clamp(p,0,1)));
            }, () => member.SetValue(target,initial))
            {
                Duration=groupDuration??animation.Duration.TimeSpan, BeginDelay=clockTimeline.BeginTime!.Value, AutoReverse=clockTimeline.AutoReverse, SpeedRatio=clockTimeline.SpeedRatio,
                RepeatCount=clockTimeline.RepeatBehavior.Count, RepeatDuration=clockTimeline.RepeatBehavior.HasDuration ? clockTimeline.RepeatBehavior.Duration : null,
                HoldEnd=clockTimeline.FillBehavior == FillBehavior.HoldEnd
            };
            clock.Completed += (_, _) => animation.Complete();
            target.TrackAnimation(name,clock);
            return clock;
        }
    }
}
