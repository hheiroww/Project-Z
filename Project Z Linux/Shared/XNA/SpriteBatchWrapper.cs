using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.XNA {

    public class SpriteBatchWrapper : IDisposable {

        public SpriteBatch SpriteBatch {
            get {
                if (_SpriteBatch is null && _graphicsDevice != null) {
                    _SpriteBatch = new SpriteBatch(_graphicsDevice);
                    _ownsSpriteBatch = true;
                }
                return _SpriteBatch;
            }
            set {
                _SpriteBatch = value;
            }
        }

        private SpriteBatch _SpriteBatch;
        private GraphicsDevice _graphicsDevice;
        private bool _ownsSpriteBatch = false;

        public GraphicsDevice GraphicsDevice {
            get {
                return SpriteBatch.GraphicsDevice;
            }
        }

        public SpriteBatchPropertySet Settings {
            get {
                return _Settings;
            }
            set {
                _Settings = value;
                _HasSettings = _Settings is null;
            }
        }
        private SpriteBatchPropertySet _Settings;
        private bool _HasSettings = false;

        public bool isRendering { get; set; } = false;

        public void Begin() {
            if (isRendering)
                return;
            if (_HasSettings) {
                Settings.Begin(SpriteBatch);
            } else {
                SpriteBatch.Begin();
            }
            isRendering = true;
        }

        public void Begin(SpriteBatchPropertySet Settings) {
            if (isRendering)
                return;
            Settings.Begin(SpriteBatch);
            isRendering = true;
        }

        public void Begin(SpriteSortMode SpriteSortMode, BlendState BlendState) {
            SpriteBatch.Begin(SpriteSortMode, BlendState);
            isRendering = true;
        }

        public void Begin(SpriteSortMode SpriteSortMode, BlendState BlendState, SamplerState SamplerState, DepthStencilState DepthStencilState, RasterizerState RasterizerState) {
            SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState);
            isRendering = true;
        }

        public void Begin(SpriteSortMode SpriteSortMode, BlendState BlendState, SamplerState SamplerState, DepthStencilState DepthStencilState, RasterizerState RasterizerState, Effect Effect) {
            SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect);
            isRendering = true;
        }

        public void Draw(Texture2D texture, Rectangle rectangle, Color color) {
            try {
                SpriteBatch.Draw(texture, rectangle, color);
            } catch (Exception ex) {

            }
        }
        public void Draw(Texture2D texture, Vector2 position, Color color) {
            try {
                SpriteBatch.Draw(texture, position, color);
            } catch (Exception ex) {

            }
        }
        public void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color) {
            try {
                SpriteBatch.Draw(texture, destinationRectangle, sourceRectangle, color);
            } catch (Exception ex) {

            }
        }
        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color) {
            try {
                SpriteBatch.Draw(texture, position, sourceRectangle, color);
            } catch (Exception ex) {

            }
        }
        public void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effect, float depth) {
            try {
                SpriteBatch.Draw(texture, destinationRectangle, sourceRectangle, color, rotation, origin, effect, depth);
            } catch (Exception ex) {

            }
        }
        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, float depth) {
            try {
                SpriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effect, depth);
            } catch (Exception ex) {

            }
        }
        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effect, float depth) {
            try {
                SpriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effect, depth);
            } catch (Exception ex) {

            }
        }
        public void DrawString(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color) {
            try {
                SpriteBatch.DrawString(spriteFont, text, position, color);
            } catch (Exception ex) {

            }
        }
        public void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color) {
            try {
                SpriteBatch.DrawString(spriteFont, text, position, color);
            } catch (Exception ex) {

            }
        }
        public void DrawString(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth) {
            try {
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effects, depth);
            } catch (Exception ex) {

            }
        }
        public void DrawString(SpriteFont spriteFont, StringBuilder text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effect, float depth) {
            try {
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effect, depth);
            } catch (Exception ex) {

            }
        }
        public void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effect, float depth) {
            try {
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effect, depth);
            } catch (Exception ex) {

            }
        }
        public void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth) {
            try {
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effects, depth);
            } catch (Exception ex) {

            }
        }

        public void End() {
            if (!isRendering)
                return;
            SpriteBatch.End();
            isRendering = false;
        }

        public SpriteBatchWrapper(SpriteBatch SpriteBatch) {
            this.SpriteBatch = SpriteBatch;
            _graphicsDevice = SpriteBatch?.GraphicsDevice;
            _ownsSpriteBatch = false;
        }

        public SpriteBatchWrapper(GraphicsDevice graphicsDevice) {
            _graphicsDevice = graphicsDevice;
        }

        public SpriteBatchWrapper(GraphicsDevice graphicsDevice, SpriteBatchPropertySet Settings) {
            _graphicsDevice = graphicsDevice;
            this.Settings = Settings;
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // Only dispose the SpriteBatch if we created it
                    if (_ownsSpriteBatch && _SpriteBatch != null) {
                        _SpriteBatch.Dispose();
                    }
                    _SpriteBatch = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

}