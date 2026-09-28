using System;
using Microsoft.Xna.Framework;

namespace ProjectZ.Shared.Animations.Properties {

    #region Property Classes

    public class WidthProperty : ElementProperty {

        public WidthProperty() : base() {
        }

        public WidthProperty(ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
        }

        protected internal override object GetValue() {
            return TargetElement.Size.X;
        }

        protected internal override void SetValue(object Value) {
            TargetElement.Size = new Vector2(Convert.ToSingle(Value), (int)Math.Round(TargetElement.Size.Y));
        }
    }
    public class HeightProperty : ElementProperty {

        public HeightProperty() : base() {
        }

        public HeightProperty(ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
        }

        protected internal override object GetValue() {
            return TargetElement.Size.Y;
        }

        protected internal override void SetValue(object Value) {
            TargetElement.Size = new Vector2((int)Math.Round(TargetElement.Size.X), Convert.ToSingle(Value));
        }
    }

    public class LeftProperty : ElementProperty {

        public LeftProperty() : base() {
        }

        public LeftProperty(ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
        }

        protected internal override object GetValue() {
            return TargetElement.Position.X;
        }

        protected internal override void SetValue(object Value) {
            TargetElement.Position = new Vector2(Convert.ToSingle(Value), (int)Math.Round(TargetElement.Position.Y));
        }
    }
    public class TopProperty : ElementProperty {

        public TopProperty() : base() {
        }

        public TopProperty(ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
        }

        protected internal override object GetValue() {
            return TargetElement.Position.Y;
        }

        protected internal override void SetValue(object Value) {
            TargetElement.Position = new Vector2(TargetElement.Position.X, Convert.ToSingle(Value));
        }
    }

    public class BackgroundColorProperty : ElementProperty {

        private ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement CastedElement;

        public BackgroundColorProperty() : base() {
        }

        public BackgroundColorProperty(ref ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
            CastedElement = (ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement)TargetElement;
        }

        protected internal override object GetValue() {
            return CastedElement.BackgroundColor;
        }

        protected internal override void SetValue(object Value) {
            CastedElement.BackgroundColor = (Color)Value;
        }
    }
    public class ForegroundColorProperty : ElementProperty {

        internal ProjectZ.Shared.Drawing.UI.Primitives.TextElement CastedElement;

        public ForegroundColorProperty() : base() {
        }

        public ForegroundColorProperty(ref ProjectZ.Shared.Drawing.UI.Primitives.TextElement TargetElement) : base(TargetElement) {
            CastedElement = TargetElement;
        }

        protected internal override object GetValue() {
            return CastedElement.ForegroundColor;
        }

        protected internal override void SetValue(object Value) {
            CastedElement.ForegroundColor = (Color)Value;
        }
    }
    public class FillColorProperty : ElementProperty {

        private ProjectZ.Shared.Drawing.UI.Advanced.PolygonElement CastedElement;

        public FillColorProperty() : base() {
        }

        public FillColorProperty(ref ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
            CastedElement = (ProjectZ.Shared.Drawing.UI.Advanced.PolygonElement)TargetElement;
        }

        protected internal override object GetValue() {
            return CastedElement.FillColor;
        }

        protected internal override void SetValue(object Value) {
            CastedElement.FillColor = (Color)Value;
        }
    }

    public class MouseOverBackgroundColorProperty : ElementProperty {

        private ProjectZ.Shared.Drawing.UI.Input.Button CastedElement;

        public MouseOverBackgroundColorProperty() : base() {
        }

        public MouseOverBackgroundColorProperty(ref ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
            CastedElement = (ProjectZ.Shared.Drawing.UI.Input.Button)TargetElement;
        }

        protected internal override object GetValue() {
            return CastedElement.MouseOverBackgroundColor;
        }

        protected internal override void SetValue(object Value) {
            CastedElement.MouseOverBackgroundColor = (Color)Value;
        }
    }
    public class MouseDownBackgroundColorProperty : ElementProperty {

        private ProjectZ.Shared.Drawing.UI.Input.Button CastedElement;

        public MouseDownBackgroundColorProperty() : base() {
        }

        public MouseDownBackgroundColorProperty(ref ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
            CastedElement = (ProjectZ.Shared.Drawing.UI.Input.Button)TargetElement;
        }

        protected internal override object GetValue() {
            return CastedElement.MouseDownBackgroundColor;
        }

        protected internal override void SetValue(object Value) {
            CastedElement.MouseDownBackgroundColor = (Color)Value;
        }
    }

    public class SpriteProgressProperty : ElementProperty {

        private ProjectZ.Shared.Drawing.UI.Advanced.SpriteElement CastedElement;

        public SpriteProgressProperty() : base() {
        }

        public SpriteProgressProperty(ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
            CastedElement = (ProjectZ.Shared.Drawing.UI.Advanced.SpriteElement)TargetElement;
        }

        protected internal override object GetValue() {
            return CastedElement.CurrentFrame;
        }

        protected internal override void SetValue(object Value) {
            CastedElement.CurrentFrame = Convert.ToInt32(Value);
        }
    }

    public class TrackbarValueProperty : ElementProperty {

        private ProjectZ.Shared.Drawing.UI.Input.Trackbar CastedElement;

        public TrackbarValueProperty() : base() {
        }

        public TrackbarValueProperty(ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) : base(TargetElement) {
            CastedElement = (ProjectZ.Shared.Drawing.UI.Input.Trackbar)TargetElement;
        }

        protected internal override object GetValue() {
            return CastedElement.Value;
        }

        protected internal override void SetValue(object Value) {
            CastedElement.Value = Convert.ToDouble(Value);
        }
    }

    #endregion

    public abstract class ElementProperty {

        internal ProjectZ.Shared.Drawing.UI.SceneElement TargetElement;

        protected internal abstract object GetValue();

        protected internal abstract void SetValue(object Value);

        public ElementProperty(ProjectZ.Shared.Drawing.UI.SceneElement TargetElement) {
            this.TargetElement = TargetElement;
        }

        public ElementProperty() {
        }

    }

}