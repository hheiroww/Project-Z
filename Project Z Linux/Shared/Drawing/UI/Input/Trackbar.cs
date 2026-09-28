using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.UI.Input {
    [Serializable]
    public class Trackbar : RectangleElement {

        #region Properties

        public bool ShowTooltip { get; set; } = true;

        public double Value {
            get {
                return _Value;
            }
            set {
                _Value = Math.Max(_MinimumValue, Math.Min(value, _MaximumValue));
                ValueChanged?.Invoke(_Value);
            }
        }
        private double _Value = 0d;

        public double MinimumValue {
            get {
                return _MinimumValue;
            }
            set {
                _MinimumValue = value;
                _Value = Math.Max(_MinimumValue, value);
            }
        }
        private double _MinimumValue = 0d;

        public double MaximumValue {
            get {
                return _MaximumValue;
            }
            set {
                _MaximumValue = value;
                _Value = Math.Min(_MaximumValue, value);
            }
        }
        private double _MaximumValue = 100d;

        public string Font {
            get {
                return _Font;
            }
            set {
                _Font = value;
                SetInternalChildProperties();
            }
        }
        private string _Font = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(12);

        public bool FixedInterval { get; set; } = true;

        public bool ShowValue { get; set; } = true;

        public virtual string GetValueDisplayText() {
            if (FixedInterval) {
                return string.Format("{0:00}", new[] { _Value });
            } else {
                return string.Format("{0:00.00}", new[] { _Value });
            }
        }

        #endregion

        #region Events

        public event ValueChangedEventHandler ValueChanged;

        public delegate void ValueChangedEventHandler(double value);
        #endregion

        #region Event Handlers

        private void Trackbar_MouseMove(Point currentPoint, Point lastPoint) {
            if (MouseDownCheck()) {
                double RelativeX = (double)(currentPoint.X - (Position.X + Slider.Size.X / 2f));
                double TranslatedValue = ProjectZ.Shared.Animations.DoubleAnimation.Interpolate(RelativeX, (double)Padding.Left, (double)(Size.X - (Slider.Size.X + Padding.Left + Padding.Right)), MinimumValue, MaximumValue);
                if (FixedInterval) {
                    TranslatedValue = Math.Round(TranslatedValue);
                    if (TranslatedValue % 2d != 0d) {
                        Value = TranslatedValue - 1d;
                    } else {
                        Value = TranslatedValue;
                    }
                } else {
                    Value = TranslatedValue;
                }
            }
        }

        private void SetInternalChildProperties() {

            // Slider Bar
            SliderBar.Size = new Vector2(Size.Y / 4f);

            // Slider
            double TranslatedX = ProjectZ.Shared.Animations.DoubleAnimation.Interpolate(Value, MinimumValue, MaximumValue, 0d, (double)(Size.X - (Slider.Size.X + Padding.Left + Padding.Right)));
            if (double.IsNaN(TranslatedX))
                TranslatedX = 8d;
            Slider.Size = new Vector2(36f);
            Slider.Margin = new Thickness((int)Math.Round(TranslatedX), (int)Math.Round(Slider.Size.Y / 2f - Size.Y / 2f), 0, 0);

            // Display Text
            if (DisplayText.isVisible) {
                string DisplayString = GetValueDisplayText();
                var DisplayTextSize = Scene.MeasureText(Font, DisplayString);
                DisplayText.Margin = new Thickness(Slider.Margin.Left + Math.Abs((int)Math.Round(DisplayTextSize.X / 2f - Slider.Size.X / 2f)), (int)Math.Round(-DisplayTextSize.Y), 0, 0);
                DisplayText.Text = DisplayString;
            }

        }

        #endregion

        #region Child Elements

        internal RectangleElement SliderBar;
        internal RectangleElement Slider;
        internal TextElement DisplayText;

        #endregion

        #region Animation Properties

        public ProjectZ.Shared.Animations.Properties.TrackbarValueProperty ValueProperty;

        #endregion

        #region Constructors

        public Trackbar(Scene Scene) : base(Scene) {
            SliderBar = new RectangleElement(this.Scene, spriteBatch.SpriteBatch) {
                VerticalAlign = VerticalAlignment.Center,
                HorizontalAlign = HorizontalAlignment.Stretch,
                zIndex = -10
            };
            Slider = new RectangleElement(this.Scene, spriteBatch.SpriteBatch) {
                BackgroundColor = new Color(100, 100, 100),
                VerticalAlign = VerticalAlignment.Stretch,
                zIndex = -5
            };
            DisplayText = new TextElement(this.Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                OrientationReserve = DisplayReservation.FloatBoth,
                zIndex = 0
            };
            ValueProperty = new ProjectZ.Shared.Animations.Properties.TrackbarValueProperty(this);
            Init();
            SetInternalChildProperties();
            RectangleChanged += SetInternalChildProperties;
            ValueChanged += (_) => SetInternalChildProperties();
        }

        public Trackbar(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            SliderBar = new RectangleElement(this.Scene, this.spriteBatch.SpriteBatch) {
                VerticalAlign = VerticalAlignment.Center,
                HorizontalAlign = HorizontalAlignment.Stretch,
                zIndex = -10
            };
            Slider = new RectangleElement(this.Scene, this.spriteBatch.SpriteBatch) {
                BackgroundColor = new Color(100, 100, 100),
                VerticalAlign = VerticalAlignment.Stretch,
                zIndex = -5
            };
            DisplayText = new TextElement(this.Scene, this.spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                OrientationReserve = DisplayReservation.FloatBoth,
                zIndex = 0
            };
            ValueProperty = new ProjectZ.Shared.Animations.Properties.TrackbarValueProperty(this);
            Init();
            SetInternalChildProperties();
            RectangleChanged += SetInternalChildProperties;
            ValueChanged += (_) => SetInternalChildProperties();
        }

        public Trackbar(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            SliderBar = new RectangleElement(this.Scene, spriteBatch.SpriteBatch) {
                VerticalAlign = VerticalAlignment.Center,
                HorizontalAlign = HorizontalAlignment.Stretch,
                zIndex = -10
            };
            Slider = new RectangleElement(this.Scene, spriteBatch.SpriteBatch) {
                BackgroundColor = new Color(100, 100, 100),
                VerticalAlign = VerticalAlignment.Stretch,
                zIndex = -5
            };
            DisplayText = new TextElement(this.Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                OrientationReserve = DisplayReservation.FloatBoth,
                zIndex = 0
            };
            ValueProperty = new ProjectZ.Shared.Animations.Properties.TrackbarValueProperty(this);
            Init();
            SetInternalChildProperties();
            RectangleChanged += SetInternalChildProperties;
            ValueChanged += (_) => SetInternalChildProperties();
        }

        private void Init() {
            BackgroundColor = Color.Transparent;
            Padding = new Thickness(4);

            // Keep internal parts ordered relative to the Trackbar itself
            SliderBar.zIndex = zIndex - 10;
            Slider.zIndex = zIndex - 5;
            DisplayText.zIndex = zIndex;

            Children.AddRange(new SceneElement[] { SliderBar, Slider, DisplayText });
            Scene.OnMouseMove += Trackbar_MouseMove;
        }

        #endregion

        private bool MouseDownCheck() {
            return isMouseDown | SliderBar.isMouseDown | Slider.isMouseDown;
        }

        protected internal override void Draw(GameTime gameTime) {
            if (ShowValue && MouseDownCheck()) {
                DisplayText.isVisible = true;
            } else {
                DisplayText.isVisible = false;
            }
            base.Draw(gameTime);
        }

        ~Trackbar() {
            Scene.OnMouseMove -= Trackbar_MouseMove;
        }
    }

}