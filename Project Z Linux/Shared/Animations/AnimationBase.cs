using System;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Animations.Easing;

namespace ProjectZ.Shared.Animations {

    public abstract class AnimationBase {

        public event OnAnimationFinishedEventHandler OnAnimationFinished;

        public delegate void OnAnimationFinishedEventHandler(object sender);
        public EaseFunction easeFunction { get; set; }
        public TimeSpan Duration { get; set; }
        public GameTime gameTime { get; set; }
        public object From { get; set; }
        public object To { get; set; }
        public object lastValue { get; set; }
        public bool AutoRepeat { get; set; } = false;

        public bool Running {
            get {
                return _Running;
            }
        }
        private bool _Running = false;

        public abstract object Value(double t);

        public object Value() {
            return Value(time);
        }

        protected double time {
            get {
                long difference = GetDifference();
                double v = difference / (double)Duration.Ticks;
                if (difference >= Duration.Ticks) {
                    Stop();
                    RaiseOnFinished(this);
                }
                return v;
            }
        }

        private long GetDifference() {
            long CurrentTick = gameTime.TotalGameTime.Ticks;
            return CurrentTick - StartTick;
        }

        private long StartTick = 0L;

        protected internal void RaiseOnFinished(object sender) {
            if (AutoRepeat) {
                Start();
            } else {
                OnAnimationFinished?.Invoke(sender);
            }
        }

        protected void Init(EaseFunction EaseFunction, object From, object To, TimeSpan Duration, GameTime gameTime, bool Autostart) {
            easeFunction = EaseFunction;
            this.From = From;
            this.To = To;
            this.Duration = Duration;
            this.gameTime = gameTime;
            StartTick = gameTime.TotalGameTime.Ticks;
            if (Autostart)
                Start();
        }

        public void Start() {
            StartTick = gameTime.TotalGameTime.Ticks;
            _Running = true;
        }

        public void Stop() {
            _Running = false;
        }

    }

}