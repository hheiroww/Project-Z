using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Animations.Properties;

namespace ProjectZ.Shared.Animations {

    public class Timeline {

        private Dictionary<AnimationBase, ElementProperty> ActiveAnimations = new Dictionary<AnimationBase, ElementProperty>();
        private List<AnimationBase> InactiveAnimations = new List<AnimationBase>();
        private List<object[]> BindQueue = new List<object[]>();

        public GameTime gameTime { get; set; }

        public void AddChild(AnimationBase Animation, ElementProperty TargetProperty) {
            if (Animation.From == null)
                Animation.From = TargetProperty.GetValue();
            BindQueue.Add(new object[] { Animation, TargetProperty });
        }

        public void RemoveChild(AnimationBase Animation) {
            if (isChild(Animation) && !InactiveAnimations.Contains(Animation)) {
                InactiveAnimations.Add(Animation);
            }
        }

        public bool isChild(AnimationBase Animation) {
            return ActiveAnimations.ContainsKey(Animation);
        }

        public void Tick(GameTime gameTime) {

            // Hack to enable the concurrent binding of animations
            for (int i = BindQueue.Count - 1; i >= 0; i -= 1) {
                if (BindQueue[i] is null)
                    continue;
                AnimationBase Animation = (AnimationBase)BindQueue[i][0];
                ElementProperty TargetProperty = (ElementProperty)BindQueue[i][1];
                ActiveAnimations.Add(Animation, TargetProperty);
                BindQueue.RemoveAt(i);
            }

            // Continue the Animation
            foreach (AnimationBase A in ActiveAnimations.Keys) {
                if (A.Running) {
                    var TargetProperty = ActiveAnimations[A];
                    TargetProperty.SetValue(A.Value());
                    if (!A.Running)
                        A.RaiseOnFinished(this);
                }
            }

            for (int i = InactiveAnimations.Count - 1; i >= 0; i -= 1) {
                var A = InactiveAnimations[i];
                ActiveAnimations.Remove(A);
                InactiveAnimations.RemoveAt(i);
            }

        }

        public Timeline(GameTime gameTime) {
            this.gameTime = gameTime;
        }
    }

}