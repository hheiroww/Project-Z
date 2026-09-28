using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.UI.Layout {

    /// <summary>
    /// Orientation for layout panels.
    /// </summary>
    public enum Orientation {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// A stack panel control similar to WPF StackPanel.
    /// Arranges child elements in a single line (horizontal or vertical).
    /// </summary>
    [Serializable]
    public class StackPanel : RectangleElement {

        #region Properties

        /// <summary>
        /// Gets or sets the orientation of the stack panel.
        /// </summary>
        public Orientation Orientation {
            get {
                return _Orientation;
            }
            set {
                _Orientation = value;
                ArrangeChildren();
            }
        }
        private Orientation _Orientation = Orientation.Vertical;

        /// <summary>
        /// Gets or sets the spacing between child elements.
        /// </summary>
        public float Spacing {
            get {
                return _Spacing;
            }
            set {
                _Spacing = value;
                ArrangeChildren();
            }
        }
        private float _Spacing = 4.0f;

        /// <summary>
        /// Gets or sets whether to automatically resize to fit children.
        /// </summary>
        public bool AutoSize { get; set; } = false;

        #endregion

        #region Events

        /// <summary>
        /// Raised when the layout is updated.
        /// </summary>
        public event LayoutUpdatedEventHandler LayoutUpdated;

        public delegate void LayoutUpdatedEventHandler();

        #endregion

        #region Constructors

        public StackPanel(Scene Scene) : base(Scene) {
            Init();
            RectangleChanged += OnLayoutChanged;
        }

        public StackPanel(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init();
            RectangleChanged += OnLayoutChanged;
        }

        public StackPanel(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            Init();
            RectangleChanged += OnLayoutChanged;
        }

        private void Init() {
            BackgroundColor = Color.Transparent;
            Children.ChildAdded += OnChildAdded;
            Children.ChildRemoved += OnChildRemoved;
        }

        #endregion

        #region Methods

        private void OnChildAdded(SceneElement c) {
            ArrangeChildren();
        }

        private void OnChildRemoved(SceneElement c) {
            ArrangeChildren();
        }

        private void OnLayoutChanged() {
            ArrangeChildren();
        }

        /// <summary>
        /// Arranges child elements according to the orientation.
        /// </summary>
        public void ArrangeChildren() {
            float currentOffset = 0f;
            float maxCrossSize = 0f;

            foreach (SceneElement child in Children) {
                if (!child.isVisible)
                    continue;

                if (_Orientation == Orientation.Vertical) {
                    child.Position = new Vector2(Padding.Left, Padding.Top + currentOffset);
                    currentOffset += child.Size.Y + _Spacing;
                    maxCrossSize = Math.Max(maxCrossSize, child.Size.X);
                } else {
                    child.Position = new Vector2(Padding.Left + currentOffset, Padding.Top);
                    currentOffset += child.Size.X + _Spacing;
                    maxCrossSize = Math.Max(maxCrossSize, child.Size.Y);
                }
            }

            // Remove the last spacing
            if (currentOffset > _Spacing) {
                currentOffset -= _Spacing;
            }

            // Auto-size the panel to fit children
            if (AutoSize) {
                if (_Orientation == Orientation.Vertical) {
                    Size = new Vector2(maxCrossSize + Padding.Left + Padding.Right, currentOffset + Padding.Top + Padding.Bottom);
                } else {
                    Size = new Vector2(currentOffset + Padding.Left + Padding.Right, maxCrossSize + Padding.Top + Padding.Bottom);
                }
            }

            LayoutUpdated?.Invoke();
        }

        /// <summary>
        /// Adds a child element to the stack panel.
        /// </summary>
        public void AddChild(SceneElement element) {
            Children.Add(element);
        }

        /// <summary>
        /// Removes a child element from the stack panel.
        /// </summary>
        public void RemoveChild(SceneElement element) {
            Children.Remove(element);
        }

        /// <summary>
        /// Clears all child elements.
        /// </summary>
        public void ClearChildren() {
            Children.Clear();
            ArrangeChildren();
        }

        #endregion

    }

}