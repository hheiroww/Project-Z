using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;


namespace ProjectZ.Shared.Drawing.UI.Input {

    [Serializable]
    public class Button : RectangleElement {

        #region Properties

        public ButtonAutoSize AutoSize {
            get {
                return _AutoSize;
            }
            set {
                _AutoSize = value;
                if (_AutoSize != ButtonAutoSize.None) {
                    _OriginalSize = Size;
                    DoAutoSize();
                } else {
                    Size = _OriginalSize;
                }
            }
        }
        private ButtonAutoSize _AutoSize = ButtonAutoSize.XY;
        private Vector2 _OriginalSize;

        public bool isAnimated { get; set; } = false;

        public string Text {
            get {
                return _Text;
            }
            set {
                string oldText = _Text;
                _Text = value;
                OnTextChanged?.Invoke(oldText, _Text);
                Button_Changed();
            }
        }
        private string _Text = string.Empty;
        public string Font {
            get {
                return _Font;
            }
            set {
                _Font = value;
                Button_Changed();
            }
        }
        private string _Font = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(12);
        public Color ForegroundColor {
            get {
                return _ForegroundColor;
            }
            set {
                _ForegroundColor = value;
                if (TextElement != null) {
                    TextElement.ForegroundColor = _ForegroundColor;
                }
            }
        }
        private Color _ForegroundColor = Color.White;
        public override Color BackgroundColor {
            get {
                return _BackgroundColor;
            }
            set {
                _BackgroundColor = value;
            }
        }
        private Color _BackgroundColor = new Color(50, 50, 50);

        public Color MouseOverBackgroundColor {
            get {
                return _MouseOverBackgroundColor;
            }
            set {
                _MouseOverBackgroundColor = value;
            }
        }
        private Color _MouseOverBackgroundColor = new Color(90, 90, 90);

        public Color MouseDownBackgroundColor {
            get {
                return _MouseDownBackgroundColor;
            }
            set {
                _MouseDownBackgroundColor = value;
            }
        }
        private Color _MouseDownBackgroundColor = new Color(0, 150, 255);

        #endregion

        #region Events

        public event OnTextChangedEventHandler OnTextChanged;

        public delegate void OnTextChangedEventHandler(string oldText, string newText);

        #endregion

        #region Animation Properties

        public ProjectZ.Shared.Animations.Properties.MouseOverBackgroundColorProperty MouseOverBackgroundProperty { get; set; } = new ProjectZ.Shared.Animations.Properties.MouseOverBackgroundColorProperty();
        public ProjectZ.Shared.Animations.Properties.MouseDownBackgroundColorProperty MouseDownBackgroundProperty { get; set; } = new ProjectZ.Shared.Animations.Properties.MouseDownBackgroundColorProperty();
        public ProjectZ.Shared.Animations.Properties.ForegroundColorProperty ForegroundProperty { get; set; } = new ProjectZ.Shared.Animations.Properties.ForegroundColorProperty();
        #region Animation Instances
        private ProjectZ.Shared.Animations.ColorAnimation MouseEnterAnimation;
        private ProjectZ.Shared.Animations.ColorAnimation MouseLeaveAnimation;

        private void OpenButton_OnMouseEnter() {
            if (isAnimated) {
                if (MouseEnterAnimation is null) {
                    MouseEnterAnimation = new ProjectZ.Shared.Animations.ColorAnimation(new ProjectZ.Shared.Animations.Easing.SineEase(ProjectZ.Shared.Animations.Easing.EaseType.EaseInOut), _BackgroundColor, _MouseOverBackgroundColor, TimeSpan.FromSeconds(0.15d), Scene.gameTime);
                    this.BindAnimation(MouseOverBackgroundProperty, MouseEnterAnimation);
                }
                MouseEnterAnimation.Start();
            }
        }

        private void OpenButton_OnMouseLeave() {
            if (isAnimated) {
                if (MouseLeaveAnimation is null) {
                    MouseLeaveAnimation = new ProjectZ.Shared.Animations.ColorAnimation(new ProjectZ.Shared.Animations.Easing.SineEase(ProjectZ.Shared.Animations.Easing.EaseType.EaseInOut), _MouseOverBackgroundColor, _BackgroundColor, TimeSpan.FromSeconds(0.15d), Scene.gameTime);
                    this.BindAnimation(this.BackgroundProperty, MouseLeaveAnimation);
                }
                MouseLeaveAnimation.Start();
            }
        }

        #endregion

        #endregion

        #region Child Elements

        private TextElement _TextElement;

        protected internal virtual TextElement TextElement {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get {
                return _TextElement;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set {
                _TextElement = value;
            }
        }

        private bool CanChange = true;
        private void Button_Changed() {
            if (CanChange && TextElement != null) {
                CanChange = false;
                if (AutoSize != ButtonAutoSize.None)
                    DoAutoSize();
                TextElement.Text = Text;
                CanChange = true;
            }
        }

        private void DoAutoSize() {
            if (Scene is null)
                return;
            switch (AutoSize) {
                case ButtonAutoSize.X: {
                        Size = new Vector2(Scene.MeasureText(Font, Text).X + Padding.Left + Padding.Right, Size.Y);
                        break;
                    }
                case ButtonAutoSize.Y: {
                        Size = new Vector2(Size.X, Scene.MeasureText(Font, Text).Y + Padding.Top + Padding.Bottom);
                        break;
                    }
                case ButtonAutoSize.XY: {
                        Size = Scene.MeasureText(Font, Text);
                        break;
                    }
            }
        }

        #endregion

        #region Constructors

        public Button(Scene Scene) : base(Scene) {
            Init();
            MouseEnter += OpenButton_OnMouseEnter;
            MouseLeave += OpenButton_OnMouseLeave;
            RectangleChanged += Button_Changed;
        }

        public Button(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init();
            MouseEnter += OpenButton_OnMouseEnter;
            MouseLeave += OpenButton_OnMouseLeave;
            RectangleChanged += Button_Changed;
        }

        public Button(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            Init();
            MouseEnter += OpenButton_OnMouseEnter;
            MouseLeave += OpenButton_OnMouseLeave;
            RectangleChanged += Button_Changed;
        }

        private void Init() {
            // Create TextElement after base constructor has set Scene and spriteBatch
            TextElement = new TextElement(Scene, spriteBatch.SpriteBatch) {
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Center,
                isMouseBypassEnabled = true,
                ForegroundColor = _ForegroundColor
            };
            Children.Add(TextElement);
            Clip = true;
            // Assign TargetElement for animation properties
            MouseOverBackgroundProperty.TargetElement = this;
            MouseDownBackgroundProperty.TargetElement = this;
            ForegroundProperty.CastedElement = TextElement;
        }

        #endregion

        protected internal override void Draw(GameTime gameTime) {
            Color DrawColor;

            if (isMouseDown) {
                DrawColor = MouseDownBackgroundColor;
            } else if (isMouseOver) {
                DrawColor = MouseOverBackgroundColor;
            } else {
                DrawColor = BackgroundColor;
            }

            spriteBatch.Draw(Texture, Rectangle, DrawColor);
            TextElement.Draw(gameTime);
        }

    }

    // Temporary
    public enum ButtonAutoSize {
        XY,
        None,
        X,
        Y
    }

}