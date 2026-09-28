using System;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Animations.Easing;

namespace ProjectZ.Shared.Animations {

    public class DoubleAnimation : AnimationBase {

        public static double Interpolate(double value, double min, double max, double start, double end) {
            return start + (end - start) / (max - min) * (value - min);
        }

        public override object Value(double t) {
            if (Running) {
                lastValue = Interpolate(easeFunction.Ease(t), 0.0d, 1.0d, Convert.ToDouble(From), Convert.ToDouble(To));
                return lastValue;
            } else {
                return lastValue;
            }
        }

        public DoubleAnimation(EaseFunction EaseFunction, double From, double To, TimeSpan Duration, GameTime gameTime) {
            Init(EaseFunction, From, To, Duration, gameTime, false);
        }

        public DoubleAnimation(EaseFunction EaseFunction, double From, double To, TimeSpan Duration, GameTime gameTime, bool Autostart) {
            Init(EaseFunction, From, To, Duration, gameTime, Autostart);
        }

    }

}