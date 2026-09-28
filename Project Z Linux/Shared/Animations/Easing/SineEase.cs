using System;

namespace ProjectZ.Shared.Animations.Easing {

    public class SineEase : EaseFunction {

        public override double Ease(double t) {
            switch (easeType) {
                case EaseType.EaseIn: {
                        return 1d - Math.Sin(1d - t) * (Math.PI / 2d);
                    }
                case EaseType.EaseOut: {
                        return Math.Sin(t * Math.Max(Math.PI - Math.PI / 2d, 0d));
                    }
                case EaseType.EaseInOut: {
                        return (Math.Sin(t * Math.PI - Math.PI / 2d) + 1d) / 2d;
                    }

                default: {
                        return t;
                    }
            }
        }

        public SineEase(EaseType easeType) : base(easeType) {
        }

    }

}