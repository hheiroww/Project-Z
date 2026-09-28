using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.UI.Input {

    /// <summary>
    /// A radio button control similar to WPF RadioButton.
    /// Only one RadioButton in a group can be selected at a time.
    /// </summary>
    [Serializable]
    public class RadioButton : RectangleElement {

        #region Shared State

        /// <summary>
        /// Dictionary tracking radio button groups.
        /// </summary>
        private static readonly Dictionary<string, List<RadioButton>> Groups = new Dictionary<string, List<RadioButton>>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether this radio button is checked.
        /// </summary>
        public bool IsChecked {
            get {
                return _IsChecked;
            }
            set {
                if (_IsChecked != value) {
                    if (value) {
                        // Uncheck others in the same group
                        UnselectOthersInGroup();
                    }
                    _IsChecked = value;
                    UpdateCheckVisual();
                    CheckedChanged?.Invoke(_IsChecked);
                    if (_IsChecked) {
                        Checked?.Invoke();
                    } else {
                        Unchecked?.Invoke();
                    }
                }
            }
        }
        private bool _IsChecked = false;

        /// <summary>
        /// Gets or sets the group name. Radio buttons with the same group name are mutually exclusive.
        /// </summary>
        public string GroupName {
            get {
                return _GroupName;
            }
            set {
                // Remove from old group
                RemoveFromGroup();
                _GroupName = value ?? string.Empty;
                // Add to new group
                AddToGroup();
            }
        }
        private string _GroupName = string.Empty;

        /// <summary>
        /// Gets or sets the text content.
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
        /// Gets or sets the font.
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
                if (ContentText != null) {
                    ContentText.ForegroundColor = _ForegroundColor;
                }
            }
        }
        private Color _ForegroundColor = Color.White;

        /// <summary>
        /// Gets or sets the selected indicator color.
        /// </summary>
        public Color SelectedColor {
            get {
                return _SelectedColor;
            }
            set {
                _SelectedColor = value;
                UpdateCheckVisual();
            }
        }
        private Color _SelectedColor = new Color(0, 120, 215);

        /// <summary>
        /// Gets or sets the border color.
        /// </summary>
        public Color BorderColor {
            get {
                return _BorderColor;
            }
            set {
                _BorderColor = value;
                OuterCircle.FillColor = _BorderColor;
            }
        }
        private Color _BorderColor = new Color(100, 100, 100);

        /// <summary>
        /// Size of the radio button circle.
        /// </summary>
        public float CircleSize { get; set; } = 18.0f;

        #endregion

        #region Events

        /// <summary>
        /// Raised when IsChecked changes.
        /// </summary>
        public event CheckedChangedEventHandler CheckedChanged;

        public delegate void CheckedChangedEventHandler(bool isChecked);

        /// <summary>
        /// Raised when this radio button becomes checked.
        /// </summary>
        public event CheckedEventHandler Checked;

        public delegate void CheckedEventHandler();

        /// <summary>
        /// Raised when this radio button becomes unchecked.
        /// </summary>
        public event UncheckedEventHandler Unchecked;

        public delegate void UncheckedEventHandler();

        #endregion

        #region Child Elements

        private CircleElement OuterCircle;
        private CircleElement InnerCircle;
        private TextElement ContentText;

        #endregion

        #region Constructors

        public RadioButton(Scene Scene) : base(Scene) {
            Init();
            RectangleChanged += UpdateLayout;
        }

        public RadioButton(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init();
            RectangleChanged += UpdateLayout;
        }

        public RadioButton(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            Init();
            RectangleChanged += UpdateLayout;
        }

        private void Init() {
            BackgroundColor = Color.Transparent;

            // Create child elements after base constructor has set Scene and spriteBatch
            OuterCircle = new CircleElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                FillColor = _BorderColor
            };

            InnerCircle = new CircleElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                isVisible = false
            };

            ContentText = new TextElement(Scene, spriteBatch.SpriteBatch) {
                isMouseBypassEnabled = true,
                VerticalAlign = VerticalAlignment.Center,
                ForegroundColor = _ForegroundColor
            };

            Children.Add(OuterCircle);
            Children.Add(InnerCircle);
            Children.Add(ContentText);
            MouseLeftClick += OnClick;
            AddToGroup();
            UpdateLayout();
        }

        #endregion

        #region Methods

        private void OnClick(Point p) {
            if (!_IsChecked) {
                IsChecked = true;
            }
        }

        private void AddToGroup() {
            if (string.IsNullOrEmpty(_GroupName))
                return;
            lock (Groups) {
                if (!Groups.ContainsKey(_GroupName)) {
                    Groups[_GroupName] = new List<RadioButton>();
                }
                if (!Groups[_GroupName].Contains(this)) {
                    Groups[_GroupName].Add(this);
                }
            }
        }

        private void RemoveFromGroup() {
            if (string.IsNullOrEmpty(_GroupName))
                return;
            lock (Groups) {
                if (Groups.ContainsKey(_GroupName)) {
                    Groups[_GroupName].Remove(this);
                    if (Groups[_GroupName].Count == 0) {
                        Groups.Remove(_GroupName);
                    }
                }
            }
        }

        private void UnselectOthersInGroup() {
            if (string.IsNullOrEmpty(_GroupName))
                return;
            lock (Groups) {
                if (Groups.ContainsKey(_GroupName)) {
                    foreach (var radioButton in Groups[_GroupName]) {
                        if (!ReferenceEquals(radioButton, this) && radioButton._IsChecked) {
                            radioButton._IsChecked = false;
                            radioButton.UpdateCheckVisual();
                            radioButton.RaiseEvent_Unchecked();
                        }
                    }
                }
            }
        }

        private void RaiseEvent_Unchecked() {
            CheckedChanged?.Invoke(false);
            Unchecked?.Invoke();
        }

        private void UpdateLayout() {
            // Skip if child elements are not yet initialized
            if (OuterCircle is null)
                return;

            float yCenter = (Size.Y - CircleSize) / 2.0f;

            OuterCircle.Position = new Vector2(0f, yCenter);
            OuterCircle.Size = new Vector2(CircleSize, CircleSize);

            float innerSize = CircleSize * 0.5f;
            float innerOffset = (CircleSize - innerSize) / 2.0f;
            InnerCircle.Position = new Vector2(innerOffset, yCenter + innerOffset);
            InnerCircle.Size = new Vector2(innerSize, innerSize);

            ContentText.Position = new Vector2(CircleSize + 8.0f, 0f);
        }

        private void UpdateCheckVisual() {
            // Skip if child elements are not yet initialized
            if (InnerCircle is null)
                return;

            if (_IsChecked) {
                InnerCircle.isVisible = true;
                InnerCircle.FillColor = _SelectedColor;
                OuterCircle.FillColor = _SelectedColor;
            } else {
                InnerCircle.isVisible = false;
                OuterCircle.FillColor = _BorderColor;
            }
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                RemoveFromGroup();
            }
            base.Dispose(disposing);
        }

        #endregion

    }

}