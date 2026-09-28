using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.UI.Input {

    /// <summary>
    /// A checkbox control similar to WPF CheckBox.
    /// Provides IsChecked property with three-state support (checked, unchecked, indeterminate).
    /// </summary>
    [Serializable]
    public class CheckBox : RectangleElement {

        #region Properties

        /// <summary>
        /// Gets or sets whether the checkbox is checked.
        /// </summary>
        public bool? IsChecked {
            get {
                return _IsChecked;
            }
            set {
                var oldValue = _IsChecked;
                _IsChecked = value;
                if (oldValue.HasValue && _IsChecked.HasValue && oldValue.Value != _IsChecked.Value) {
                    UpdateCheckVisual();
                    CheckedChanged?.Invoke(oldValue, _IsChecked);
                    if (_IsChecked.HasValue && _IsChecked.Value == true) {
                        Checked?.Invoke();
                    } else if (_IsChecked.HasValue && _IsChecked.Value == false) {
                        Unchecked?.Invoke();
                    } else {
                        Indeterminate?.Invoke();
                    }
                }
            }
        }
        private bool? _IsChecked = false;

        /// <summary>
        /// Gets or sets whether the checkbox supports three states (checked, unchecked, indeterminate).
        /// </summary>
        public bool IsThreeState { get; set; } = false;

        /// <summary>
        /// Gets or sets the text content of the checkbox.
        /// </summary>
        public string Content {
            get {
                return _Content;
            }
            set {
                _Content = value;
                if (ContentText != null) {
                    ContentText.Text = _Content;
                    UpdateLayout();
                }
            }
        }
        private string _Content = string.Empty;

        /// <summary>
        /// Gets or sets the font for the content text.
        /// </summary>
        public string Font {
            get {
                return _Font;
            }
            set {
                _Font = value;
                if (ContentText != null) {
                    ContentText.Font = _Font;
                    UpdateLayout();
                }
            }
        }
        private string _Font = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(12);

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        public Color ForegroundColor {
            get {
                return _ForegroundColor;
            }
            set {
                _ForegroundColor = value;
                ContentText.ForegroundColor = _ForegroundColor;
            }
        }
        private Color _ForegroundColor = Color.White;

        /// <summary>
        /// Gets or sets the check mark color.
        /// </summary>
        public Color CheckColor {
            get {
                return _CheckColor;
            }
            set {
                _CheckColor = value;
                CheckMark.BackgroundColor = _CheckColor;
            }
        }
        private Color _CheckColor = Color.White;

        /// <summary>
        /// Gets or sets the box border color.
        /// </summary>
        public Color BoxBorderColor {
            get {
                return _BoxBorderColor;
            }
            set {
                _BoxBorderColor = value;
                CheckBoxBorder.BackgroundColor = _BoxBorderColor;
            }
        }
        private Color _BoxBorderColor = new Color(100, 100, 100);

        /// <summary>
        /// Gets or sets the box background color when checked.
        /// </summary>
        public Color CheckedBackgroundColor {
            get {
                return _CheckedBackgroundColor;
            }
            set {
                _CheckedBackgroundColor = value;
                UpdateCheckVisual();
            }
        }
        private Color _CheckedBackgroundColor = new Color(0, 120, 215);

        /// <summary>
        /// Size of the checkbox box.
        /// </summary>
        public float BoxSize { get; set; } = 18.0f;

        #endregion

        #region Events

        /// <summary>
        /// Raised when the IsChecked property changes.
        /// </summary>
        public event CheckedChangedEventHandler CheckedChanged;

        public delegate void CheckedChangedEventHandler(bool? oldValue, bool? newValue);

        /// <summary>
        /// Raised when the checkbox becomes checked.
        /// </summary>
        public event CheckedEventHandler Checked;

        public delegate void CheckedEventHandler();

        /// <summary>
        /// Raised when the checkbox becomes unchecked.
        /// </summary>
        public event UncheckedEventHandler Unchecked;

        public delegate void UncheckedEventHandler();

        /// <summary>
        /// Raised when the checkbox becomes indeterminate.
        /// </summary>
        public event IndeterminateEventHandler Indeterminate;

        public delegate void IndeterminateEventHandler();

        #endregion

        #region Child Elements

        private RectangleElement CheckBoxBorder;
        private RectangleElement CheckBoxBackground;
        private RectangleElement CheckMark;
        private TextElement ContentText;

        #endregion

        #region Constructors

        public CheckBox(Scene Scene) : base(Scene) {
            Init();
            RectangleChanged += UpdateLayout;
        }

        public CheckBox(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init();
            RectangleChanged += UpdateLayout;
        }

        public CheckBox(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            Init();
            RectangleChanged += UpdateLayout;
        }

        private void Init() {
            BackgroundColor = Color.Transparent;

            // Create child elements after base constructor has set Scene and spriteBatch
            CheckBoxBorder = new RectangleElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                BackgroundColor = _BoxBorderColor
            };

            CheckBoxBackground = new RectangleElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                BackgroundColor = new Color(30, 30, 30)
            };

            CheckMark = new RectangleElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                isVisible = false,
                BackgroundColor = _CheckColor
            };

            ContentText = new TextElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                VerticalAlign = VerticalAlignment.Center,
                ForegroundColor = _ForegroundColor
            };

            Children.Add(CheckBoxBorder);
            Children.Add(CheckBoxBackground);
            Children.Add(CheckMark);
            Children.Add(ContentText);
            MouseLeftClick += OnClick;
            UpdateLayout();
        }

        #endregion

        #region Methods

        private void OnClick(Point p) {
            if (IsThreeState) {
                if (_IsChecked is null) {
                    IsChecked = true;
                } else if (_IsChecked.HasValue && _IsChecked.Value == true) {
                    IsChecked = false;
                } else {
                    IsChecked = default;
                }
            } else {
                IsChecked = !_IsChecked.GetValueOrDefault(false);
            }
        }

        private void UpdateLayout() {
            // Skip if child elements are not yet initialized
            if (CheckBoxBorder is null)
                return;

            float borderThickness = 2.0f;

            CheckBoxBorder.Position = new Vector2(0f, (Size.Y - BoxSize) / 2.0f);
            CheckBoxBorder.Size = new Vector2(BoxSize, BoxSize);

            CheckBoxBackground.Position = new Vector2(borderThickness, (Size.Y - BoxSize) / 2.0f + borderThickness);
            CheckBoxBackground.Size = new Vector2(BoxSize - borderThickness * 2f, BoxSize - borderThickness * 2f);

            float checkPadding = 4.0f;
            CheckMark.Position = new Vector2(checkPadding, (Size.Y - BoxSize) / 2.0f + checkPadding);
            CheckMark.Size = new Vector2(BoxSize - checkPadding * 2f, BoxSize - checkPadding * 2f);

            ContentText.Position = new Vector2(BoxSize + 8.0f, 0f);
        }

        private void UpdateCheckVisual() {
            // Skip if child elements are not yet initialized
            if (CheckMark is null)
                return;

            if (_IsChecked.HasValue && _IsChecked.Value == true) {
                CheckMark.isVisible = true;
                CheckMark.BackgroundColor = _CheckColor;
                CheckBoxBackground.BackgroundColor = _CheckedBackgroundColor;
            } else if (_IsChecked.HasValue && _IsChecked.Value == false) {
                CheckMark.isVisible = false;
                CheckBoxBackground.BackgroundColor = new Color(30, 30, 30);
            } else {
                // Indeterminate state - show smaller mark
                CheckMark.isVisible = true;
                CheckMark.BackgroundColor = new Color(150, 150, 150);
                CheckBoxBackground.BackgroundColor = new Color(60, 60, 60);
            }
        }

        /// <summary>
        /// Toggles the checked state.
        /// </summary>
        public void Toggle() {
            OnClick(Point.Zero);
        }

        #endregion

    }

}