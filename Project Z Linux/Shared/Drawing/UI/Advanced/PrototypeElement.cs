using System;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.Drawing.UI.Advanced {

    [Serializable]
    public class PrototypeElement : Input.Button {

        #region Properties

        public SceneElement Target { get; set; }

        #endregion

        #region Child Elements


        #endregion

        #region Constructors

        public PrototypeElement(Scene Scene, SceneElement Target) : base(Scene) {
            Init(Target);
        }

        public PrototypeElement(Scene Scene, SceneElement Target, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init(Target);
        }

        public PrototypeElement(Scene Scene, SceneElement Target, bool newSpritebatch) : base(Scene, newSpritebatch) {
            Init(Target);
        }

        private void Init(SceneElement Target) {
            isPrototype = true;
            this.Target = Target;
            // Add Children
            // Children.Add()
            Clip = true;
            isVisible = false;
            Children.ForEach(c => c.isVisible = false);
        }

        #endregion

    }


}