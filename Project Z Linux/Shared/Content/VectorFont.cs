using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.VectorText;

namespace ProjectZ.Shared.Content {
    /// <summary>Compiled free outlines, drawn as GPU triangles without a font file or atlas.</summary>
    public sealed class VectorFont : IDisposable {
        readonly CompiledFont face;
        readonly float emSize;
        readonly Dictionary<string, VertexPositionColor[]> cache = new Dictionary<string, VertexPositionColor[]>();
        BasicEffect? effect;
        RasterizerState? rasterizer;
        GraphicsDevice? owner;
        public string FamilyName => CompiledFont.FamilyName;
        public string? FilePath => null;
        public int LineSpacing { get; }
        public VectorFont(float points, int weight = 400) {
            if (float.IsNaN(points) || float.IsInfinity(points) || points <= 0 || points > 512) throw new ArgumentOutOfRangeException(nameof(points));
            face = CompiledFont.ForWeight(weight); emSize = points * 96 / 72;
            LineSpacing = (int)Math.Ceiling(face.LineHeight * emSize);
        }
        public Vector2 Measure(string text) {
            var size = face.Measure(text, emSize); return new Vector2(size.X, size.Y);
        }
        public void Draw(GraphicsDevice device, string text, Vector2 position, Color color) {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!cache.TryGetValue(text, out var vertices)) {
                var data = face.GetTriangles(text, emSize);
                vertices = new VertexPositionColor[data.Length / 2];
                for (int i = 0; i < vertices.Length; i++) vertices[i] = new VertexPositionColor(new Vector3(data[i * 2], data[i * 2 + 1], 0), color);
                if (cache.Count >= 256) cache.Clear();
                cache.Add(text, vertices);
            }
            if (vertices.Length == 0) return;
            if (effect == null) {
                effect = new BasicEffect(device) { VertexColorEnabled = true };
                rasterizer = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true, MultiSampleAntiAlias = true };
                owner = device; owner.Disposing += DeviceDisposing;
            }
            for (int i = 0; i < vertices.Length; i++) vertices[i].Color = color;
            effect.World = Matrix.CreateTranslation(position.X, position.Y, 0);
            effect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1);
            var oldRasterizer = device.RasterizerState; var oldBlend = device.BlendState; var oldDepth = device.DepthStencilState;
            try {
                device.RasterizerState = oldRasterizer.ScissorTestEnable ? rasterizer : RasterizerState.CullNone;
                device.BlendState = BlendState.AlphaBlend; device.DepthStencilState = DepthStencilState.None;
                foreach (var pass in effect.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3); }
            } finally { device.RasterizerState = oldRasterizer; device.BlendState = oldBlend; device.DepthStencilState = oldDepth; }
        }
        void DeviceDisposing(object sender, EventArgs args) => Dispose();
        public void Dispose() {
            if (owner != null) owner.Disposing -= DeviceDisposing;
            owner = null; effect?.Dispose(); effect = null; rasterizer?.Dispose(); rasterizer = null; cache.Clear();
        }
    }
}
