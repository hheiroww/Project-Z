using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Drawing.UI.Advanced;
using ProjectZ.Shared.Drawing.UI.Primitives;
using ProjectZ.Shared.Extensions;

namespace ProjectZ.Shared.Drawing.UI.Input {
    [Serializable]
    public class Textbox : RectangleElement {

        #region Properties

        public Vector2 TextPadding {
            get {
                return -_TextPadding;
            }
            set {
                _TextPadding = -value;
            }
        }
        private Vector2 _TextPadding = new Vector2(-2, -1);
        public HorizontalAlignment HorizontalTextAlignment {
            get {
                return _HorizontalTextAlignment;
            }
            set {
                _HorizontalTextAlignment = value;
                UpdateTextbox();
            }
        }
        private HorizontalAlignment _HorizontalTextAlignment = HorizontalAlignment.Left;
        public VerticalAlignment VerticalTextAlignment {
            get {
                return _VerticalTextAlignment;
            }
            set {
                _VerticalTextAlignment = value;
                UpdateTextbox();
            }
        }
        private VerticalAlignment _VerticalTextAlignment = VerticalAlignment.Top;
        public bool AcceptsReturn { get; set; } = true;
        public int SelectionLength {
            get {
                return _SelectionLength;
            }
            set {
                _SelectionLength = value;
                UpdateTextbox();
            }
        }
        private int _SelectionLength = 0;
        public int SelectionStart {
            get {
                return _SelectionStart;
            }
            set {
                _SelectionStart = Math.Max(value, 0);
                UpdateTextbox();
            }
        }
        private int _SelectionStart = 0;
        public int CaretPosition {
            get {
                return _CaretPosition;
            }
            set {
                _CaretPosition = Math.Min(Text.Length, Math.Max(0, value));
                UpdateTextbox();
            }
        }
        private int _CaretPosition = 0;
        public string Text {
            get {
                return _Text;
            }
            set {
                OnTextChanged?.Invoke(new TextChangedEventArgs(_Text, value));
                _Text = value;
                UpdateTextbox();
            }
        }
        private string _Text = string.Empty;
        public string Font {
            get {
                return _Font;
            }
            set {
                _Font = value;
                _FontCharHeight = Scene.MeasureText(_Font, "A").Y;
                UpdateTextbox();
            }
        }
        private string _Font = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(12);
        private float _FontCharHeight = 0f;
        public Color ForegroundColor {
            get {
                return _ForegroundColor;
            }
            set {
                _ForegroundColor = value;
                TextElement.ForegroundColor = _ForegroundColor;
                Caret.BackgroundColor = _ForegroundColor;
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

        #endregion

        #region Internals

        private float LastCaretPos = 0f;
        private bool canNotUpdate = false;
        private Vector2 GetCaretPosition() {
            var ResolvedCaretPoint = TextElement.CharIndexToPoint(CaretPosition);

            var v = new Vector2(ResolvedCaretPoint.X, ResolvedCaretPoint.Y);

            if (LastCaretPos != v.Y) {
                float diff = v.Y - LastCaretPos;
                LastCaretPos = v.Y;
            }

            return v;
        }

        private Vector2[] GetSelectionVectors() {
            var Vectors = new List<Vector2>();
            var ResolvedStartPoint = TextElement.CharIndexToPoint(SelectionStart).ToVector2().Subtract(-TextPadding);
            var CurrentPosition = new Vector2(ResolvedStartPoint.X, ResolvedStartPoint.Y);



            return Vectors.ToArray();
        }

        private Vector2 GetAlignment(SceneElement RelativeTo) {
            Vector2 GetAlignmentRet = default;
            float RelativeSizeY = Math.Max(_FontCharHeight, RelativeTo.Size.Y);
            switch (VerticalTextAlignment) {
                case VerticalAlignment.Center: {
                        GetAlignmentRet.Y = Size.Y / 2f - RelativeSizeY / 2f;
                        break;
                    }
                case VerticalAlignment.Bottom: {
                        GetAlignmentRet.Y = Size.Y - RelativeSizeY;
                        break;
                    }

                default: {
                        GetAlignmentRet.Y = 0f;
                        break;
                    }
            }
            switch (HorizontalTextAlignment) {
                case HorizontalAlignment.Center: {
                        GetAlignmentRet.X = Size.X / 2f - RelativeTo.Size.X / 2f;
                        break;
                    }
                case HorizontalAlignment.Right: {
                        GetAlignmentRet.X = Size.X - RelativeTo.Size.X;
                        break;
                    }

                default: {
                        GetAlignmentRet.X = 0f;
                        break;
                    }
            }

            return GetAlignmentRet;
        }

        private string GetLastLine() {
            int LastReturn = Text.LastIndexOf(Environment.NewLine);
            return LastReturn < 0 ? null : Text.Remove(0, Text.LastIndexOf(Environment.NewLine));
        }

        private bool IsNewLineNeeded(float widthControl, string text) {
            var textSize = Scene.MeasureText(Font, text);

            if (textSize.X > widthControl - 20f) {
                return true;
            } else {
                return false;
            }

        }

        private void UpdateTextbox() {
            if (canNotUpdate)
                return;

            canNotUpdate = true;

            // Set Text Properties
            TextElement.Text = Text;
            TextElement.Font = Font;
            TextElement.Position = GetAlignment(TextElement);

            // Set Caret Properties
            Caret.Position = GetCaretPosition();
            Caret.Size = new Vector2(1f, Scene.MeasureText(Font, "A").Y);
            Caret.BackgroundColor = Color.Transparent;

            // Set Selection Properties
            Selection.ClearVectorPoints();
            if (SelectionLength != 0) {
                Selection.AddVectorPoints(GetSelectionVectors());
            }

            canNotUpdate = false;

        }

        #endregion

        #region Events

        public event OnPreTextInputEventHandler OnPreTextInput;

        public delegate void OnPreTextInputEventHandler(PreTextInputEventArgs e);

        public event OnTextChangedEventHandler OnTextChanged;

        public delegate void OnTextChangedEventHandler(TextChangedEventArgs e);

        #endregion

        #region Animation Properties

        public ProjectZ.Shared.Animations.Properties.ForegroundColorProperty ForegroundProperty;

        #region Animation Instances
        private ProjectZ.Shared.Animations.ColorAnimation _CaretFadeInAnimation;

        private ProjectZ.Shared.Animations.ColorAnimation CaretFadeInAnimation {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get {
                return _CaretFadeInAnimation;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set {
                if (_CaretFadeInAnimation != null) {
                    _CaretFadeInAnimation.OnAnimationFinished -= CaretFadeInAnimation_OnAnimationFinished;
                }

                _CaretFadeInAnimation = value;
                if (_CaretFadeInAnimation != null) {
                    _CaretFadeInAnimation.OnAnimationFinished += CaretFadeInAnimation_OnAnimationFinished;
                }
            }
        }
        private ProjectZ.Shared.Animations.ColorAnimation _CaretFadeOutAnimation;

        private ProjectZ.Shared.Animations.ColorAnimation CaretFadeOutAnimation {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get {
                return _CaretFadeOutAnimation;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set {
                if (_CaretFadeOutAnimation != null) {
                    _CaretFadeOutAnimation.OnAnimationFinished -= CaretFadeOutAnimation_OnAnimationFinished;
                }

                _CaretFadeOutAnimation = value;
                if (_CaretFadeOutAnimation != null) {
                    _CaretFadeOutAnimation.OnAnimationFinished += CaretFadeOutAnimation_OnAnimationFinished;
                }
            }
        }

        private void ShowCaret() {
            if (CaretFadeOutAnimation != null) {
                if (CaretFadeOutAnimation.Running) {
                    CaretFadeOutAnimation.Stop();
                }
            }
            if (CaretFadeInAnimation is null) {
                CaretFadeInAnimation = new ProjectZ.Shared.Animations.ColorAnimation(new ProjectZ.Shared.Animations.Easing.SineEase(ProjectZ.Shared.Animations.Easing.EaseType.EaseInOut), Caret.BackgroundColor, _ForegroundColor, TimeSpan.FromSeconds(0.5d), Scene.gameTime);
                this.BindAnimation(Caret.BackgroundProperty, CaretFadeInAnimation);
            }
            CaretFadeInAnimation.Start();
        }

        private void HideCaret() {
            if (CaretFadeInAnimation != null) {
                if (CaretFadeInAnimation.Running) {
                    CaretFadeInAnimation.Stop();
                }
            }
            if (CaretFadeOutAnimation is null) {
                CaretFadeOutAnimation = new ProjectZ.Shared.Animations.ColorAnimation(new ProjectZ.Shared.Animations.Easing.SineEase(ProjectZ.Shared.Animations.Easing.EaseType.EaseInOut), Color.Transparent, Color.Transparent, TimeSpan.FromSeconds(0.25d), Scene.gameTime);
                this.BindAnimation(Caret.BackgroundProperty, CaretFadeOutAnimation);
            }
            CaretFadeOutAnimation.Start();
        }

        private void CaretFadeOutAnimation_OnAnimationFinished(object sender) {
            if (CanSelect & isSelected) {
                ShowCaret();
            }
        }

        private void CaretFadeInAnimation_OnAnimationFinished(object sender) {
            HideCaret();
        }

        #endregion

        #endregion

        #region Child Elements

        protected internal TextElement TextElement;
        protected internal RectangleElement Caret;
        protected internal PolygonElement Selection;

        #endregion

        #region Constructors

        public Textbox(Scene Scene) : base(Scene, true) {
            TextElement = new TextElement(this.Scene, spriteBatch.SpriteBatch);
            Caret = new RectangleElement(this.Scene, spriteBatch.SpriteBatch);
            Selection = new PolygonElement(this.Scene, Array.Empty<Vector2>(), spriteBatch.SpriteBatch);

            SceneElement argTargetElement = this;
            this.BackgroundProperty = new ProjectZ.Shared.Animations.Properties.BackgroundColorProperty(ref argTargetElement);
            ForegroundProperty = new ProjectZ.Shared.Animations.Properties.ForegroundColorProperty(ref TextElement);
            CanSelect = true;
            _FontCharHeight = Scene.MeasureText(_Font, "A").Y;

            Selection.spriteBatch.Settings = new ProjectZ.Shared.XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.Additive);
            /* TODO ERROR: Skipped IfDirectiveTrivia
            #If WINDOWS Then
            */
            Selection.FillColor = new Color(0f, 0f, 1f, 0.4f);
            /* TODO ERROR: Skipped ElifDirectiveTrivia
            #ElseIf LINUX Then
            *//* TODO ERROR: Skipped DisabledTextTrivia
                        Selection.FillColor = New Color(0, 0, 1, 102)
            *//* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
            spriteBatch.Settings = new ProjectZ.Shared.XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend, (SamplerState)null, (DepthStencilState)null, new RasterizerState() { ScissorTestEnable = true });
            Children.AddRange(new SceneElement[] { TextElement, Caret, Selection });
            Clip = true;
            RectangleChanged += UpdateTextbox;
            OnKeyPress += Textbox_OnKeyPress;
            MouseLeftClick += Textbox_OnMouseLeftClick;
            MouseDrag += Textbox_OnMouseDrag;
            Selected += Textbox_OnSelect;
            Deselected += Textbox_OnDeselect;
        }

        #endregion

        // Disable automatic spriteBatch Initialization
        protected internal override void doDraw(GameTime gameTime) {

            // Draw Background normally
            if (isElementSpriteBatch) {
                spriteBatch.Begin();
                base.Draw(gameTime);
                spriteBatch.End();
            } else {
                base.Draw(gameTime);
            }

            spriteBatch.Begin();
            Draw(gameTime);
            spriteBatch.End();
        }

        public override void Tick(GameTime gameTime) {
            base.Tick(gameTime);
        }

        private void Textbox_OnKeyPress(Keys Key, KeyboardState KeyboardState) {
            var PreInputEvent = new PreTextInputEventArgs(Key, KeyboardState);
            OnPreTextInput?.Invoke(PreInputEvent);
            if (!PreInputEvent.Cancel) {
                bool control = KeyboardState.IsKeyDown(Keys.LeftControl) | KeyboardState.IsKeyDown(Keys.RightControl);
                bool Execute = true;

                if (control) {
                    switch (Key) {
                        case Keys.A: {
                                SelectionStart = 0;
                                SelectionLength = Text.Length;
                                CaretPosition = SelectionStart + SelectionLength;
                                Execute = false;
                                break;
                            }
                    }
                }

                if (Execute) {
                    if (Key == Keys.Enter && !AcceptsReturn)
                        return;
                    if (SelectionLength + SelectionStart > Text.Length) {
                        SelectionStart = 0;
                        SelectionLength = 0;
                    }
                    switch (Key) {
                        case Keys.Back: {
                                if (SelectionLength != 0) {
                                    Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length));
                                    CaretPosition = SelectionStart;
                                } else if (Text.Length > 0 & CaretPosition > 0) {
                                    int CharCount = Text[CaretPosition - 1].ToString() == Environment.NewLine ? 2 : 1;
                                    Text = Text.Remove(CaretPosition - CharCount, CharCount);
                                    CaretPosition -= CharCount;
                                }

                                break;
                            }
                        case Keys.Delete: {
                                if (SelectionLength != 0) {
                                    Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length));
                                    CaretPosition = SelectionStart;
                                } else if (Text.Length > 0 & CaretPosition < Text.Length) {
                                    int CharCount = Text[CaretPosition].ToString() == Environment.NewLine ? 2 : 1;
                                    Text = Text.Remove(CaretPosition, CharCount);
                                }

                                break;
                            }
                        case Keys.Left: {
                                if (CaretPosition == 0)
                                    break;
                                if (SelectionLength != 0) {
                                    SelectionLength = 0;
                                }
                                if (KeyboardState.IsKeyDown(Keys.LeftControl) | KeyboardState.IsKeyDown(Keys.RightControl)) {
                                    CaretPosition = SelectionStart;
                                    break;
                                } else {
                                    CaretPosition -= Text[CaretPosition - 1].ToString() == Environment.NewLine ? 2 : 1;
                                }

                                break;
                            }
                        case Keys.Right: {
                                if (string.IsNullOrEmpty(Text))
                                    break;
                                if (SelectionLength != 0) {
                                    SelectionLength = 0;
                                }
                                if (KeyboardState.IsKeyDown(Keys.LeftControl) | KeyboardState.IsKeyDown(Keys.RightControl)) {
                                    CaretPosition = Text.Length;
                                    break;
                                } else if (CaretPosition < Text.Length) {
                                    int CharCount = Text[Math.Min(Text.Length, CaretPosition)].ToString() == Environment.NewLine ? 2 : 1;
                                    CaretPosition += CharCount;
                                }

                                break;
                            }

                        default: {
                                string newInput = SceneManager.TryConvertKeyboardInput(Key, KeyboardState);
                                if (!string.IsNullOrEmpty(newInput)) {
                                    if (SelectionLength != 0) {
                                        Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length));
                                        CaretPosition = SelectionStart;
                                        Text = Text.Insert(CaretPosition, newInput);
                                        CaretPosition += newInput.Length;
                                        SelectionStart = 0;
                                        SelectionLength = 0;
                                    } else {
                                        string LastLine = GetLastLine();

                                        if (!string.IsNullOrEmpty(LastLine) && IsNewLineNeeded(Size.X, LastLine)) {
                                            newInput += Environment.NewLine;
                                        }
                                        Text = Text.Insert(Math.Min(CaretPosition, Text.Length), newInput);
                                        CaretPosition += newInput.Length;
                                    }
                                }

                                break;
                            }
                    }
                }

            }
        }

        private void Textbox_OnMouseLeftClick(Point p) {
            var RelativePoint = RelativeTo(TextElement);
            var FixedPoint = new Point(RelativePoint.X + p.X + 3, RelativePoint.Y + p.Y);
            CaretPosition = TextElement.PointToCharIndex(FixedPoint);
            SelectionStart = 0;
            SelectionLength = 0;
        }

        private void Textbox_OnMouseDrag(Point currentPoint, Point startPoint) {
            var RelativePoint = RelativeTo(TextElement);
            var FixedStartPoint = new Point(RelativePoint.X + startPoint.X + 3, RelativePoint.Y + startPoint.Y);
            int StartIndex = TextElement.PointToCharIndex(FixedStartPoint);

            var FixedEndPoint = new Point(RelativePoint.X + currentPoint.X + 3, RelativePoint.Y + currentPoint.Y);
            int EndIndex = TextElement.PointToCharIndex(FixedEndPoint);

            if (StartIndex > EndIndex) {
                SelectionStart = EndIndex;
                CaretPosition = StartIndex;
                SelectionLength = StartIndex - EndIndex;
            } else {
                SelectionStart = StartIndex;
                CaretPosition = EndIndex;
                SelectionLength = EndIndex - StartIndex;
            }

        }

        private void Textbox_OnSelect() {
            ShowCaret();
        }

        private void Textbox_OnDeselect() {
            HideCaret();
        }

    }

    public class PreTextInputEventArgs {

        public Keys Key {
            get {
                return _Key;
            }
        }
        private Keys _Key;

        public KeyboardState KeyboardState {
            get {
                return _KeyboardState;
            }
        }
        private KeyboardState _KeyboardState;

        public bool Cancel { get; set; } = false;

        public PreTextInputEventArgs(Keys Key, KeyboardState KeyboardState) {
            _Key = Key;
            _KeyboardState = KeyboardState;
        }

    }

    public class TextChangedEventArgs {

        public string OldText {
            get {
                return _OldText;
            }
        }
        private string _OldText;

        public string NewText {
            get {
                return _NewText;
            }
        }
        private string _NewText;

        public TextChangedEventArgs(string Oldtext, string NewText) {
            _OldText = Oldtext;
            _NewText = NewText;
        }

    }

}