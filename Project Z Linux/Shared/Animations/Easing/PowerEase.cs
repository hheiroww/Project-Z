using System;

namespace ProjectZ.Shared.Animations.Easing {

    public class PowerEase : EaseFunction {

        public double Power { get; set; } = 2d;

        public override double Ease(double t) {
            switch (easeType) {
                case EaseType.EaseIn: {
                        return Math.Pow(t, Power);
                    }
                case EaseType.EaseOut: {
                        return Math.Pow(t, 1d - Math.Pow(t, Power));
                    }

                default: {
                        return t;
                    }
            }
        }

        public PowerEase(EaseType easeType) : base(easeType) {
        }

        public PowerEase(EaseType easeType, double Power) : base(easeType) {
            this.Power = Power;
        }

    }

}