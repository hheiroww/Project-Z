using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.XNA {

    public class SpriteBatchPropertySet {

        public SpriteSortMode SpriteSortMode { get; set; }
        public BlendState BlendState { get; set; }
        public SamplerState SamplerState { get; set; }
        public DepthStencilState DepthStencilState { get; set; }
        public RasterizerState RasterizerState { get; set; }
        public Effect Effect {
            get {
                return _Effect;
            }
            set {
                _Effect = value;
                if (value != null) {
                    _UseEffect = true;
                } else {
                    _UseEffect = false;
                }
            }
        }
        private Effect _Effect;

        public bool UseEffect {
            get {
                return _UseEffect;
            }
        }
        private bool _UseEffect = false;

        public void Begin(SpriteBatch SpriteBatch) {
            if (UseEffect) {
                SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect);
            } else {
                SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState);
            }
        }

        public SpriteBatchPropertySet(SpriteSortMode SpriteSortMode, BlendState BlendState) {
            this.SpriteSortMode = SpriteSortMode;
            this.BlendState = BlendState;
        }

        public SpriteBatchPropertySet(SpriteSortMode SpriteSortMode, BlendState BlendState, SamplerState SamplerState, DepthStencilState DepthStencilState, RasterizerState RasterizerState) {
            this.SpriteSortMode = SpriteSortMode;
            this.BlendState = BlendState;
            this.SamplerState = SamplerState;
            this.DepthStencilState = DepthStencilState;
            this.RasterizerState = RasterizerState;
        }

        public SpriteBatchPropertySet(SpriteSortMode SpriteSortMode, BlendState BlendState, SamplerState SamplerState, DepthStencilState DepthStencilState, RasterizerState RasterizerState, Effect Effect) {
            this.SpriteSortMode = SpriteSortMode;
            this.BlendState = BlendState;
            this.SamplerState = SamplerState;
            this.DepthStencilState = DepthStencilState;
            this.RasterizerState = RasterizerState;
            this.Effect = Effect;
        }
    }

}