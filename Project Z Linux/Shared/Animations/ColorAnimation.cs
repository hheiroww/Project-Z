using System;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Animations.Easing;

namespace ProjectZ.Shared.Animations {

    public class ColorAnimation : AnimationBase {

        public ColorAnimation(EaseFunction EaseFunction, Color From, Color To, TimeSpan Duration, GameTime gameTime) {
            Init(EaseFunction, From, To, Duration, gameTime, false);
        }

        public ColorAnimation(EaseFunction EaseFunction, Color From, Color To, TimeSpan Duration, GameTime gameTime, bool Autostart) {
            Init(EaseFunction, From, To, Duration, gameTime, Autostart);
        }

        public override object Value(double t) {
            if (Running) {
                lastValue = Color.Lerp((Color)From, (Color)To, (float)easeFunction.Ease(t));
                return lastValue;
            } else {
                return lastValue;
            }
        }

    }

}