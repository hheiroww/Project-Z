using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.Drawing.UI.Primitives {

    /// <summary>
    /// Specifies how text wrapping occurs.
    /// </summary>
    public enum TextWrapping {
        /// <summary>
        /// No wrapping - text extends beyond container bounds.
        /// </summary>
        NoWrap,
        /// <summary>
        /// Wraps text at word boundaries to fit within container width.
        /// </summary>
        Wrap,
        /// <summary>
        /// Wraps text at character boundaries when words don't fit.
        /// </summary>
        WrapWithOverflow
    }

    [Serializable]
    public class TextElement : SceneElement {

        #region Properties

        public string Text {
            get {
                return _Text;
            }
            set {
                _Text = value;
                UpdateWrappedText();
                TextChanged?.Invoke();
            }
        }
        private string _Text = string.Empty;

        /// <summary>
        /// Gets or sets the text wrapping behavior.
        /// </summary>
        public TextWrapping TextWrapping {
            get {
                return _TextWrapping;
            }
            set {
                _TextWrapping = value;
                UpdateWrappedText();
            }
        }
        private TextWrapping _TextWrapping = TextWrapping.Wrap;

        /// <summary>
        /// Gets or sets the maximum width for text wrapping.
        /// If 0, uses parent container width or no limit.
        /// </summary>
        public float MaxWidth {
            get {
                return _MaxWidth;
            }
            set {
                _MaxWidth = value;
                UpdateWrappedText();
            }
        }
        private float _MaxWidth = 0f;

        /// <summary>
        /// Gets the wrapped text for rendering.
        /// </summary>
        private string WrappedText { get; set; } = string.Empty;

        /// <summary>
        /// Prevents recursive updates when Size changes trigger RectangleChanged.
        /// </summary>
        private bool _isUpdatingText = false;

        public string Font {
            get {
                return _Font;
            }
            set {
                _Font = value;
                UpdateWrappedText();
            }
        }
        private string _Font = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(12);

        public virtual Color ForegroundColor { get; set; } = new Color(255, 255, 255);

        #endregion

        #region Events

        public event TextChangedEventHandler TextChanged;

        public delegate void TextChangedEventHandler();

        #endregion

        #region Text Wrapping

        /// <summary>
        /// Updates the wrapped text based on current settings.
        /// </summary>
        private void UpdateWrappedText() {
            // Prevent recursive calls
            if (_isUpdatingText)
                return;
            _isUpdatingText = true;

            try {
                // Skip if Scene != yet initialized
                if (Scene is null) {
                    WrappedText = _Text;
                    return;
                }

                if (_TextWrapping == TextWrapping.NoWrap || string.IsNullOrEmpty(_Text)) {
                    WrappedText = _Text;
                    Size = Scene.MeasureText(Font, string.IsNullOrEmpty(_Text) ? " " : _Text);
                    Invalidate();
                    return;
                }

                // Determine the wrap width using this priority:
                // 1. Explicit MaxWidth property
                // 2. MaxSize.X constraint (from XAML MaxWidth on the element)
                // 3. Parent available width (minus padding and margin)
                // 4. No limit
                float wrapWidth = _MaxWidth;
                if (wrapWidth <= 0f && MaxSize.X < float.MaxValue) {
                    wrapWidth = MaxSize.X;
                }
                if (wrapWidth <= 0f && Parent != null && Parent.Size.X > 0f) {
                    wrapWidth = Parent.Size.X - Parent.Padding.Left - Parent.Padding.Right - Margin.Left - Margin.Right;
                }
                if (wrapWidth <= 0f) {
                    wrapWidth = 9999.0f; // No limit
                }

                WrappedText = WrapText(_Text, wrapWidth);
                var measuredSize = Scene.MeasureText(Font, string.IsNullOrEmpty(WrappedText) ? " " : WrappedText);
                // Constrain width so text element never reports wider than its wrap boundary
                Size = new Vector2(Math.Min(measuredSize.X, wrapWidth), measuredSize.Y);
                Invalidate();
            } finally {
                _isUpdatingText = false;
            }
        }

        /// <summary>
        /// Wraps text to fit within the specified width.
        /// </summary>
        private string WrapText(string text, float maxWidth) {
            if (string.IsNullOrEmpty(text))
                return text;

            var result = new StringBuilder();
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.None);

            for (int lineIndex = 0, loopTo = lines.Length - 1; lineIndex <= loopTo; lineIndex++) {
                string line = lines[lineIndex];
                if (lineIndex > 0)
                    result.AppendLine();

                if (string.IsNullOrEmpty(line))
                    continue;

                float lineWidth = Scene.MeasureText(Font, line).X;
                if (lineWidth <= maxWidth) {
                    result.Append(line);
                    continue;
                }

                // Need to wrap this line
                if (_TextWrapping == TextWrapping.Wrap) {
                    result.Append(WrapLineByWords(line, maxWidth));
                } else { // WrapWithOverflow
                    result.Append(WrapLineByCharacters(line, maxWidth));
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Wraps a single line by word boundaries.
        /// </summary>
        private string WrapLineByWords(string line, float maxWidth) {
            var result = new StringBuilder();
            string[] words = line.Split(' ');
            var currentLine = new StringBuilder();

            foreach (string word in words) {
                string testLine = currentLine.Length == 0 ? word : currentLine.ToString() + " " + word;
                float testWidth = Scene.MeasureText(Font, testLine).X;

                if (testWidth <= maxWidth) {
                    if (currentLine.Length > 0)
                        currentLine.Append(" ");
                    currentLine.Append(word);
                } else {
                    // Word doesn't fit - start new line
                    if (currentLine.Length > 0) {
                        result.AppendLine(currentLine.ToString());
                        currentLine.Clear();
                    }

                    // Check if single word is too long
                    float wordWidth = Scene.MeasureText(Font, word).X;
                    if (wordWidth > maxWidth) {
                        // Break the word by characters
                        result.Append(WrapLineByCharacters(word, maxWidth));
                        if (result.Length > 0 && !result.ToString().EndsWith(Environment.NewLine)) {
                            result.AppendLine();
                        }
                    } else {
                        currentLine.Append(word);
                    }
                }
            }

            if (currentLine.Length > 0) {
                result.Append(currentLine.ToString());
            }

            return result.ToString().TrimEnd(Environment.NewLine.ToCharArray());
        }

        /// <summary>
        /// Wraps a single line by character boundaries.
        /// </summary>
        private string WrapLineByCharacters(string line, float maxWidth) {
            var result = new StringBuilder();
            var currentLine = new StringBuilder();

            foreach (char c in line) {
                string testLine = currentLine.ToString() + c;
                float testWidth = Scene.MeasureText(Font, testLine).X;

                if (testWidth <= maxWidth) {
                    currentLine.Append(c);
                } else {
                    // Character doesn't fit - start new line
                    if (currentLine.Length > 0) {
                        result.AppendLine(currentLine.ToString());
                        currentLine.Clear();
                    }
                    currentLine.Append(c);
                }
            }

            if (currentLine.Length > 0) {
                result.Append(currentLine.ToString());
            }

            return result.ToString();
        }

        private void OnParentChanged() {
            if (_TextWrapping != TextWrapping.NoWrap) {
                UpdateWrappedText();
            }
        }

        #endregion

        public int PointToCharIndex(Point p) {
            // Use the rendered (wrapped) text for hit testing
            string textToCheck = _TextWrapping == TextWrapping.NoWrap ? Text : WrappedText;
            if (string.IsNullOrEmpty(textToCheck))
                return 0;

            int charIndex = 0;
            float startX = Position.X;
            float startY = Position.Y;
            float x = startX;
            float y = startY;
            string[] lines = textToCheck.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                string line = lines[lineIdx];
                x = startX;
                float lineHeight = Scene.MeasureText(Font, "A").Y;
                for (int i = 0; i < line.Length; i++)
                {
                    string ch = line[i].ToString();
                    var charSize = Scene.MeasureText(Font, ch);
                    var charRect = new Rectangle((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(charSize.X), (int)Math.Round(lineHeight));
                    if (charRect.Contains(p))
                    {
                        // If click is past 1/2 width, return next index
                        if (p.X > charRect.X + charRect.Width / 2)
                            return charIndex + 1;
                        else
                            return charIndex;
                    }
                    x += charSize.X;
                    charIndex++;
                }
                // Handle clicking in the area after the last character in the line
                var endRect = new Rectangle((int)Math.Round(x), (int)Math.Round(y), 1, (int)Math.Round(lineHeight));
                if (endRect.Contains(p))
                    return charIndex;
                y += lineHeight;
                // For all but last line, count the newline char
                if (lineIdx < lines.Length - 1)
                    charIndex++;
            }
            // If point is after all text, return length
            return charIndex;
        }


        public Point CharIndexToPoint(int i) {
            // Use the rendered (wrapped) text for position calculation
            string textToCheck = _TextWrapping == TextWrapping.NoWrap ? Text : WrappedText;
            if (string.IsNullOrEmpty(textToCheck) || i <= 0)
                return new Point((int)Position.X, (int)Position.Y);

            int charIndex = 0;
            float startX = Position.X;
            float startY = Position.Y;
            float x = startX;
            float y = startY;
            string[] lines = textToCheck.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                string line = lines[lineIdx];
                x = startX;
                float lineHeight = Scene.MeasureText(Font, "A").Y;
                for (int j = 0; j < line.Length; j++)
                {
                    if (charIndex == i)
                        return new Point((int)Math.Round(x), (int)Math.Round(y));
                    string ch = line[j].ToString();
                    var charSize = Scene.MeasureText(Font, ch);
                    x += charSize.X;
                    charIndex++;
                }
                // After last character in line, handle newline char
                if (charIndex == i)
                    return new Point((int)Math.Round(x), (int)Math.Round(y));
                y += lineHeight;
                // For all but last line, count the newline char
                if (lineIdx < lines.Length - 1)
                    charIndex++;
            }
            // If index is after all text, return end position
            return new Point((int)Math.Round(x), (int)Math.Round(y));
        }

        public int CharIndexToLineIndex(int i) {
            // Dim cutText As String = Text.Substring(0, Math.Max(0, Math.Min(i, Text.Length)))
            // Dim NewLineIndexies As Integer() = cutText.Search(vbCrLf, 0)

            return 0; // NewLineIndexies.Length - 1
        }

        public IndexInformation GetIndexInformation(int i) {
            return new IndexInformation(i, CharIndexToPoint(i), CharIndexToLineIndex(i));
        }

        protected internal override void Draw(GameTime gameTime) {
            string textToDraw = _TextWrapping == TextWrapping.NoWrap ? Text : WrappedText;
            if (!string.IsNullOrEmpty(textToDraw)) {
                spriteBatch.DrawString(Scene.contentCollection.Fonts[Font], textToDraw, Position, ForegroundColor);
            }
        }

        #region Constructors

        public TextElement(Scene Scene) : base(Scene) {
            RectangleChanged += OnParentChanged;
        }

        public TextElement(Scene Scene, SpriteBatch spriteBatch) : base(Scene, ref spriteBatch) {
            RectangleChanged += OnParentChanged;
        }

        public TextElement(Scene Scene, bool newSpriteBatch) : base(Scene, newSpriteBatch) {
            RectangleChanged += OnParentChanged;
        }

        #endregion

        [Serializable]
        public class IndexInformation {

            public int CharIndex { get; set; }
            public Point IndexPosition { get; set; }
            public int LineIndex { get; set; }

            public IndexInformation(int CharIndex, Point IndexPosition, int LineIndex) {
                this.CharIndex = CharIndex;
                this.IndexPosition = IndexPosition;
                this.LineIndex = LineIndex;
            }

        }

    }
}