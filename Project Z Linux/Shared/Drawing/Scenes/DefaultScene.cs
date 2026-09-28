using System;

#region Using Statements
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ {

#endregion

    public class DefaultScene : Scene {

        public DefaultScene(ref SceneManager SceneManager) : base(SceneManager) {

            // Initialize Settings

            isCursorVisible = false;
            Initialized += Me_Initialized;

        }

        private void Me_Initialized(GameTime gameTime) {
            var TextElement1 = new TextElement(this) {
                Text = "Please initialize the SceneManager Class." + Environment.NewLine + "Instructions:" + Environment.NewLine + "1. Find the LoadContent() method. ( zWindow.vb )" + Environment.NewLine + "2. Define a Scene. EX: Dim Scene As New MainScene(sceneManager)" + Environment.NewLine + "3. Add the scene to the scene manager. EX: SceneManager.AddScene(\"MainScene\", Scene)" + Environment.NewLine + "4. Set the scene to Active. EX: SceneManager.ActiveScene = Scene",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Center
            };

            AddElement(TextElement1);
            AddElement(TextElement1);
            AddElement(TextElement1);
        }

    }
}