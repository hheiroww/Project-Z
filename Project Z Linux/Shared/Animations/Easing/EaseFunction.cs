
namespace ProjectZ.Shared.Animations.Easing {

    public abstract class EaseFunction {

        public abstract double Ease(double t);

        public EaseType easeType { get; set; }

        public EaseFunction(EaseType easeType) {
            this.easeType = easeType;
        }
    }

}