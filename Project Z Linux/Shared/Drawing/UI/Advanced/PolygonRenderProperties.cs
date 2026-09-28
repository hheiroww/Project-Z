using Microsoft.Xna.Framework;

namespace ProjectZ.Shared.Drawing.UI.Advanced {

    public class PolygonRenderProperties {

        public event OnWireFrameColorChangedEventHandler OnWireFrameColorChanged;

        public delegate void OnWireFrameColorChangedEventHandler(Color c);

        public bool WireFrame { get; set; } = false;

        public bool FillPolygon { get; set; } = false;

        public bool HighlightOnMouseOver { get; set; } = false;

        public Color HighlightColor { get; set; } = Color.Red;

        public bool HighQuality { get; set; } = false;

        public double MinAngle { get; set; } = 20d;

        public Color WireFrameColor {
            get {
                return _WireFrameColor;
            }
            set {
                _WireFrameColor = value;
                OnWireFrameColorChanged?.Invoke(_WireFrameColor);
            }
        }
        private Color _WireFrameColor = Color.Cyan;

    }

}