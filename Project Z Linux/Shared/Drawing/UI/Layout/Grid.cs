using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.UI.Layout {

    /// <summary>
    /// Represents a row or column definition for a Grid panel.
    /// </summary>
    [Serializable]
    public class GridDefinition {
        /// <summary>
        /// Gets or sets the size of this row/column.
        /// Use -1 for Auto (fit to content), 0 for Star (proportional), positive for fixed pixels.
        /// </summary>
        public float Size { get; set; } = 0f; // Star by default

        /// <summary>
        /// Gets or sets the star multiplier when Size = 0 (Star mode).
        /// </summary>
        public float Star { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the minimum size.
        /// </summary>
        public float MinSize { get; set; } = 0f;

        /// <summary>
        /// Gets or sets the maximum size.
        /// </summary>
        public float MaxSize { get; set; } = float.MaxValue;

        /// <summary>
        /// The calculated actual size after layout.
        /// </summary>
        internal float ActualSize = 0f;

        /// <summary>
        /// Creates an Auto-sized definition.
        /// </summary>
        public static GridDefinition Auto() {
            return new GridDefinition() { Size = -1 };
        }

        /// <summary>
        /// Creates a Star-sized definition.
        /// </summary>
        public static GridDefinition StarSize(float multiplier = 1.0f) {
            return new GridDefinition() { Size = 0f, Star = multiplier };
        }

        /// <summary>
        /// Creates a fixed-sized definition.
        /// </summary>
        public static GridDefinition Fixed(float pixels) {
            return new GridDefinition() { Size = pixels };
        }
    }

    /// <summary>
    /// Attached properties for Grid children.
    /// </summary>
    [Serializable]
    public class GridAttached {
        public int Row { get; set; } = 0;
        public int Column { get; set; } = 0;
        public int RowSpan { get; set; } = 1;
        public int ColumnSpan { get; set; } = 1;
    }

    /// <summary>
    /// A grid panel control similar to WPF Grid.
    /// Arranges child elements in rows and columns.
    /// </summary>
    [Serializable]
    public class Grid : RectangleElement {

        #region Properties

        /// <summary>
        /// Gets the row definitions.
        /// </summary>
        public List<GridDefinition> RowDefinitions {
            get {
                return _RowDefinitions;
            }
        }
        private readonly List<GridDefinition> _RowDefinitions = new List<GridDefinition>();

        /// <summary>
        /// Gets the column definitions.
        /// </summary>
        public List<GridDefinition> ColumnDefinitions {
            get {
                return _ColumnDefinitions;
            }
        }
        private readonly List<GridDefinition> _ColumnDefinitions = new List<GridDefinition>();

        /// <summary>
        /// Gets or sets whether to show grid lines (for debugging).
        /// </summary>
        public bool ShowGridLines { get; set; } = false;

        /// <summary>
        /// Gets or sets the grid line color.
        /// </summary>
        public Color GridLineColor { get; set; } = new Color(100, 100, 100);

        /// <summary>
        /// Attached properties for each child.
        /// </summary>
        private readonly Dictionary<SceneElement, GridAttached> _attachedProperties = new Dictionary<SceneElement, GridAttached>();

        #endregion

        #region Events

        /// <summary>
        /// Raised when the layout is updated.
        /// </summary>
        public event LayoutUpdatedEventHandler LayoutUpdated;

        public delegate void LayoutUpdatedEventHandler();

        #endregion

        #region Constructors

        public Grid(Scene Scene) : base(Scene) {
            Init();
            RectangleChanged += OnLayoutChanged;
        }

        public Grid(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init();
            RectangleChanged += OnLayoutChanged;
        }

        public Grid(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            Init();
            RectangleChanged += OnLayoutChanged;
        }

        private void Init() {
            BackgroundColor = Color.Transparent;
            Children.ChildAdded += OnChildAdded;
            Children.ChildRemoved += OnChildRemoved;
        }

        #endregion

        #region Attached Property Methods

        /// <summary>
        /// Sets the row for a child element.
        /// </summary>
        public void SetRow(SceneElement element, int row) {
            EnsureAttached(element).Row = row;
            ArrangeChildren();
        }

        /// <summary>
        /// Gets the row for a child element.
        /// </summary>
        public int GetRow(SceneElement element) {
            return GetAttached(element).Row;
        }

        /// <summary>
        /// Sets the column for a child element.
        /// </summary>
        public void SetColumn(SceneElement element, int column) {
            EnsureAttached(element).Column = column;
            ArrangeChildren();
        }

        /// <summary>
        /// Gets the column for a child element.
        /// </summary>
        public int GetColumn(SceneElement element) {
            return GetAttached(element).Column;
        }

        /// <summary>
        /// Sets the row span for a child element.
        /// </summary>
        public void SetRowSpan(SceneElement element, int rowSpan) {
            EnsureAttached(element).RowSpan = Math.Max(1, rowSpan);
            ArrangeChildren();
        }

        /// <summary>
        /// Gets the row span for a child element.
        /// </summary>
        public int GetRowSpan(SceneElement element) {
            return GetAttached(element).RowSpan;
        }

        /// <summary>
        /// Sets the column span for a child element.
        /// </summary>
        public void SetColumnSpan(SceneElement element, int columnSpan) {
            EnsureAttached(element).ColumnSpan = Math.Max(1, columnSpan);
            ArrangeChildren();
        }

        /// <summary>
        /// Gets the column span for a child element.
        /// </summary>
        public int GetColumnSpan(SceneElement element) {
            return GetAttached(element).ColumnSpan;
        }

        private GridAttached EnsureAttached(SceneElement element) {
            if (!_attachedProperties.ContainsKey(element)) {
                _attachedProperties[element] = new GridAttached();
            }
            return _attachedProperties[element];
        }

        private GridAttached GetAttached(SceneElement element) {
            if (_attachedProperties.ContainsKey(element)) {
                return _attachedProperties[element];
            }
            return new GridAttached();
        }

        #endregion

        #region Methods

        private void OnChildAdded(SceneElement c) {
            EnsureAttached(c);
            ArrangeChildren();
        }

        private void OnChildRemoved(SceneElement c) {
            _attachedProperties.Remove(c);
            ArrangeChildren();
        }

        private void OnLayoutChanged() {
            ArrangeChildren();
        }

        /// <summary>
        /// Adds a child element at the specified grid position.
        /// </summary>
        public void AddChild(SceneElement element, int row, int column, int rowSpan = 1, int columnSpan = 1) {
            var attached = EnsureAttached(element);
            attached.Row = row;
            attached.Column = column;
            attached.RowSpan = rowSpan;
            attached.ColumnSpan = columnSpan;
            Children.Add(element);
        }

        /// <summary>
        /// Arranges child elements in the grid.
        /// </summary>
        public void ArrangeChildren() {
            // Ensure at least one row and column
            if (_RowDefinitions.Count == 0) {
                _RowDefinitions.Add(GridDefinition.StarSize(1f));
            }
            if (_ColumnDefinitions.Count == 0) {
                _ColumnDefinitions.Add(GridDefinition.StarSize(1f));
            }

            // Calculate row heights
            CalculateSizes(_RowDefinitions, Size.Y - Padding.Top - Padding.Bottom);

            // Calculate column widths
            CalculateSizes(_ColumnDefinitions, Size.X - Padding.Left - Padding.Right);

            // Position children
            foreach (SceneElement child in Children) {
                if (!child.isVisible)
                    continue;

                var attached = GetAttached(child);
                int row = Math.Min(attached.Row, _RowDefinitions.Count - 1);
                int col = Math.Min(attached.Column, _ColumnDefinitions.Count - 1);
                int rowSpan = Math.Min(attached.RowSpan, _RowDefinitions.Count - row);
                int colSpan = Math.Min(attached.ColumnSpan, _ColumnDefinitions.Count - col);

                // Calculate position
                float x = Padding.Left;
                for (int i = 0, loopTo = col - 1; i <= loopTo; i++)
                    x += _ColumnDefinitions[i].ActualSize;

                float y = Padding.Top;
                for (int i = 0, loopTo1 = row - 1; i <= loopTo1; i++)
                    y += _RowDefinitions[i].ActualSize;

                // Calculate size based on span
                float width = 0f;
                for (int i = col, loopTo2 = col + colSpan - 1; i <= loopTo2; i++)
                    width += _ColumnDefinitions[i].ActualSize;

                float height = 0f;
                for (int i = row, loopTo3 = row + rowSpan - 1; i <= loopTo3; i++)
                    height += _RowDefinitions[i].ActualSize;

                child.Position = new Vector2(x, y);
                child.Size = new Vector2(width, height);
            }

            LayoutUpdated?.Invoke();
        }

        private void CalculateSizes(List<GridDefinition> definitions, float availableSize) {
            float fixedTotal = 0f;
            float starTotal = 0f;

            // First pass: calculate fixed and auto sizes
            foreach (var def in definitions) {
                if (def.Size > 0f) {
                    // Fixed size
                    def.ActualSize = Math.Min(def.MaxSize, Math.Max(def.MinSize, def.Size));
                    fixedTotal += def.ActualSize;
                } else if (def.Size < 0f) {
                    // Auto size - for now, treat as minimum size
                    def.ActualSize = def.MinSize;
                    fixedTotal += def.ActualSize;
                } else {
                    // Star size
                    starTotal += def.Star;
                }
            }

            // Second pass: distribute remaining space to star sizes
            float remaining = availableSize - fixedTotal;
            if (remaining > 0f && starTotal > 0f) {
                float starUnit = remaining / starTotal;
                foreach (var def in definitions) {
                    if (def.Size == 0f) {
                        def.ActualSize = Math.Min(def.MaxSize, Math.Max(def.MinSize, starUnit * def.Star));
                    }
                }
            }
        }

        #endregion

    }

}