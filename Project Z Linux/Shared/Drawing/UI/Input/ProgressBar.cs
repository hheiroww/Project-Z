using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.UI.Input {

    /// <summary>
    /// A progress bar control similar to WPF ProgressBar.
    /// Displays progress as a filled portion of a bar.
    /// </summary>
    [Serializable]
    public class ProgressBar : RectangleElement {

        #region Properties

        /// <summary>
        /// Gets or sets the current value of the progress bar.
        /// </summary>
        public double Value {
            get {
                return _Value;
            }
            set {
                _Value = Math.Max(_Minimum, Math.Min(value, _Maximum));
                UpdateProgress();
                ValueChanged?.Invoke(_Value);
            }
        }
        private double _Value = 0d;

        /// <summary>
        /// Gets or sets the minimum value.
        /// </summary>
        public double Minimum {
            get {
                return _Minimum;
            }
            set {
                _Minimum = value;
                UpdateProgress();
            }
        }
        private double _Minimum = 0d;

        /// <summary>
        /// Gets or sets the maximum value.
        /// </summary>
        public double Maximum {
            get {
                return _Maximum;
            }
            set {
                _Maximum = value;
                UpdateProgress();
            }
        }
        private double _Maximum = 100d;

        /// <summary>
        /// Gets or sets whether the progress bar is indeterminate (shows animation without specific progress).
        /// </summary>
        public bool IsIndeterminate {
            get {
                return _IsIndeterminate;
            }
            set {
                _IsIndeterminate = value;
                UpdateProgress();
            }
        }
        private bool _IsIndeterminate = false;

        /// <summary>
        /// Gets or sets the fill color of the progress indicator.
        /// </summary>
        public Color FillColor {
            get {
                return _FillColor;
            }
            set {
                _FillColor = value;
                ProgressFill.BackgroundColor = _FillColor;
            }
        }
        private Color _FillColor = new Color(0, 120, 215);

        /// <summary>
        /// Gets or sets the track (background) color.
        /// </summary>
        public Color TrackColor {
            get {
                return _TrackColor;
            }
            set {
                _TrackColor = value;
                BackgroundColor = _TrackColor;
            }
        }
        private Color _TrackColor = new Color(40, 40, 40);

        /// <summary>
        /// Gets or sets the border color.
        /// </summary>
        public Color BorderColor {
            get {
                return _BorderColor;
            }
            set {
                _BorderColor = value;
                BorderElement.BackgroundColor = _BorderColor;
            }
        }
        private Color _BorderColor = new Color(80, 80, 80);

        /// <summary>
        /// Gets or sets the border thickness.
        /// </summary>
        public float BorderThickness { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets whether to show the percentage text.
        /// </summary>
        public bool ShowPercentage {
            get {
                return _ShowPercentage;
            }
            set {
                _ShowPercentage = value;
                PercentageText.isVisible = _ShowPercentage;
                UpdateProgress();
            }
        }
        private bool _ShowPercentage = false;

        /// <summary>
        /// Gets or sets the font for the percentage text.
        /// </summary>
        public string Font {
            get {
                return _Font;
            }
            set {
                _Font = value;
                PercentageText.Font = _Font;
            }
        }
        private string _Font = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(10);

        /// <summary>
        /// Gets the current progress percentage (0-100).
        /// </summary>
        public double Percentage {
            get {
                if (_Maximum == _Minimum)
                    return 0d;
                return (_Value - _Minimum) / (_Maximum - _Minimum) * 100.0d;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the Value property changes.
        /// </summary>
        public event ValueChangedEventHandler ValueChanged;

        public delegate void ValueChangedEventHandler(double value);

        #endregion

        #region Child Elements

        private RectangleElement BorderElement;
        private RectangleElement ProgressFill;
        private TextElement PercentageText;

        // For indeterminate animation
        private float _indeterminateOffset = 0f;
        private readonly float _indeterminateWidth = 0.3f; // 30% of bar width

        #endregion

        #region Constructors

        public ProgressBar(Scene Scene) : base(Scene) {
            Init();
            RectangleChanged += UpdateProgress;
        }

        public ProgressBar(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init();
            RectangleChanged += UpdateProgress;
        }

        public ProgressBar(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            Init();
            RectangleChanged += UpdateProgress;
        }

        private void Init() {
            BackgroundColor = _TrackColor;

            // Create child elements after base constructor has set Scene and spriteBatch
            BorderElement = new RectangleElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                BackgroundColor = _BorderColor
            };

            ProgressFill = new RectangleElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                BackgroundColor = _FillColor
            };

            PercentageText = new TextElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Center,
                ForegroundColor = Color.White,
                isVisible = false
            };

            Children.Add(BorderElement);
            Children.Add(ProgressFill);
            Children.Add(PercentageText);
            UpdateProgress();
        }

        #endregion

        #region Methods

        private void UpdateProgress() {
            // Skip if child elements are not yet initialized
            if (BorderElement is null)
                return;

            // Update border
            BorderElement.Position = Vector2.Zero;
            BorderElement.Size = Size;

            // Calculate fill width
            float innerX = BorderThickness;
            float innerWidth = Size.X - BorderThickness * 2f;
            float innerHeight = Size.Y - BorderThickness * 2f;

            if (_IsIndeterminate) {
                // Indeterminate: animated sliding bar
                float barWidth = innerWidth * _indeterminateWidth;
                ProgressFill.Position = new Vector2(innerX + _indeterminateOffset * innerWidth, BorderThickness);
                ProgressFill.Size = new Vector2(barWidth, innerHeight);
            } else {
                // Determinate: fill based on value
                float fillRatio = (float)((_Value - _Minimum) / Math.Max(1d, _Maximum - _Minimum));
                fillRatio = Math.Max(0f, Math.Min(1f, fillRatio));
                float fillWidth = innerWidth * fillRatio;

                ProgressFill.Position = new Vector2(innerX, BorderThickness);
                ProgressFill.Size = new Vector2(fillWidth, innerHeight);
            }

            // Update percentage text
            if (_ShowPercentage && !_IsIndeterminate) {
                PercentageText.Text = string.Format("{0:0}%", Percentage);
                PercentageText.Position = new Vector2((Size.X - PercentageText.Size.X) / 2.0f, (Size.Y - PercentageText.Size.Y) / 2.0f);
            }
        }

        public override void Tick(GameTime gameTime) {
            base.Tick(gameTime);

            // Animate indeterminate progress
            if (_IsIndeterminate) {
                float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
                _indeterminateOffset += deltaSeconds * 0.5f; // Speed of animation
                if (_indeterminateOffset > 1.0f + _indeterminateWidth) {
                    _indeterminateOffset = -_indeterminateWidth;
                }
                UpdateProgress();
            }
        }

        /// <summary>
        /// Increases the value by the specified amount.
        /// </summary>
        public void Increment(double amount = 1d) {
            Value = _Value + amount;
        }

        /// <summary>
        /// Decreases the value by the specified amount.
        /// </summary>
        public void Decrement(double amount = 1d) {
            Value = _Value - amount;
        }

        #endregion

    }

}