using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ProjectZ.Shared.Drawing.UI.Advanced {

    public class SceneProjectionHost : SceneElement {

        public double Scale {
            get {
                return _Scale;
            }
            set {
                _Scale = Math.Max(0.01d, Math.Min(1d, value));
                CheckInterp();
                CanSetSize = true;
            }
        }
        private double _Scale = 1d;

        public ProjectZ.Shared.XNA.SpriteBatchPropertySet RenderOptions { get; set; }

        private bool AllowInterp = false;
        private bool CanSetSize = true;

        public Rectangle SimulatedBounds {
            get {
                return _SimulatedBounds;
            }
            set {
                _SimulatedBounds = value;
                CheckInterp();
            }
        }
        private Rectangle _SimulatedBounds = new Rectangle(0, 0, 0, 0);

        public Texture2D Texture {
            get {
                return _Texture;
            }
        }
        private Texture2D _Texture;

        public Scene TargetScene {
            get {
                return _TargetScene;
            }
            set {
                _TargetScene = value;
            }
        }
        private Scene _TargetScene;

        #region Constructors

        public SceneProjectionHost(Scene Scene, Scene TargetScene) : base(Scene) {

         
            RenderOptions = new ProjectZ.Shared.XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone);
            SimulatedBounds = new Rectangle(0, 0, TargetScene.graphicsDevice.PresentationParameters.BackBufferWidth, TargetScene.graphicsDevice.PresentationParameters.BackBufferHeight);
            Size = new Vector2(SimulatedBounds.Width, SimulatedBounds.Height);
            TargetScene.RenderTargetOptions = RenderOptions;
            TargetScene.UseRenderTarget = true;
            spriteBatch.Settings = RenderOptions;
            this.TargetScene = TargetScene;
            _Texture = TargetScene.Texture;
            Scene.UseRenderTarget = true;
            Scene.AddProjectionHost(this);
            SizeChanged += SceneProjection_OnSizeChange;
        }

        #endregion

        public Point Interp(Point p) {
            Point InterpRet = default;
            if (AllowInterp) {
                InterpRet.X = (int)Math.Round(ProjectZ.Shared.Animations.DoubleAnimation.Interpolate((double)(p.X - Position.X), 0d, (double)Rectangle.Width, 0d, (double)SimulatedBounds.Width));
                InterpRet.Y = (int)Math.Round(ProjectZ.Shared.Animations.DoubleAnimation.Interpolate((double)(p.Y - Position.Y), 0d, (double)Rectangle.Height, 0d, (double)SimulatedBounds.Height));
            } else {
                InterpRet.X = 0;
                InterpRet.Y = 0;
            }

            return InterpRet;
        }

        public MouseState Interp(MouseState mouseState) {
            var InterpolatedPoint = Interp(mouseState.Position);
            return new MouseState(InterpolatedPoint.X, InterpolatedPoint.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);
        }

        protected internal override void doDraw(GameTime gameTime) {
            TargetScene.Tick(gameTime);

            var RT = TargetScene.DrawToRenderTarget();
            var sR = new Rectangle((int)Math.Round(SimulatedBounds.X * Scale), (int)Math.Round(SimulatedBounds.Y * Scale), SimulatedBounds.Width, SimulatedBounds.Height);
            spriteBatch.Begin(RenderOptions);
            spriteBatch.Draw(RT, Rectangle, sR, Color.White);
            spriteBatch.End();
            ChangeRenderSize();
        }

        protected internal override bool ContainsPoint(Point p) {
            return TargetScene.PointToElement(p) != null;
        }

        protected internal override void Draw(GameTime gameTime) {

        }

        private void SceneProjection_OnSizeChange(Vector2 oldSize, Vector2 newSize) {
            CheckInterp();
            CanSetSize = true;
        }

        private void ChangeRenderSize() {
            if (CanSetSize) {
                CanSetSize = false;
                TargetScene.renderTarget.Dispose();
                TargetScene.renderTarget = new RenderTarget2D(TargetScene.graphicsDevice, (int)Math.Round(SimulatedBounds.Width * Scale), (int)Math.Round(SimulatedBounds.Height * Scale), false, TargetScene.graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.Depth24Stencil8);

            }
        }

        private void CheckInterp() {
            AllowInterp = Size.X > 0f & Size.Y > 0f & SimulatedBounds.Width > 0 & SimulatedBounds.Height > 0 & Rectangle.Height > 0 & Rectangle.Width > 0;
        }

        ~SceneProjectionHost() {
            Scene.RemoveProjectionHost(this);
        }
    }

}