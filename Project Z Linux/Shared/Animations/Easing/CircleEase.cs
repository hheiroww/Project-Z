using System;

namespace ProjectZ.Shared.Animations.Easing {

    public class CircleEase : EaseFunction {

        public override double Ease(double t) {
            switch (easeType) {
                case EaseType.EaseIn: {
                        return 1d - Math.Sqrt(1d - Math.Pow(t, 2d));
                    }
                case EaseType.EaseOut: {
                        return 1d - Math.Sqrt(1d - Math.Sqrt(Math.Pow(t, 2d)));
                    }
                case EaseType.EaseInOut: {
                        return 1d - Math.Sqrt(1d - Math.Pow(t, 1d - Math.Sqrt(1d - Math.Pow(t, 2d)) / 2d));
                    }

                default: {
                        return t;
                    }
            }
        }

        public CircleEase(EaseType easeType) : base(easeType) {
        }

    }

}