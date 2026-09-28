using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.UI.Layout;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.UI.Input {

    /// <summary>
    /// A ComboBox control that displays a dropdown list of items.
    /// </summary>
    public class ComboBox : RectangleElement {

        private List<string> _items = new List<string>();
        private int _selectedIndex = -1;
        private bool _isDropDownOpen = false;
        private List<RectangleElement> _dropDownItems = new List<RectangleElement>();
        private TextElement _headerText;
        private TextElement _dropDownButton;
        private float _itemHeight = 28.0f;

        public event SelectionChangedEventHandler SelectionChanged;

        public delegate void SelectionChangedEventHandler(ComboBox sender, int selectedIndex);

        public List<string> Items {
            get {
                return _items;
            }
            set {
                _items = value;
                UpdateVisual();
            }
        }

        public int SelectedIndex {
            get {
                return _selectedIndex;
            }
            set {
                if (value >= -1 && value < _items.Count) {
                    _selectedIndex = value;
                    UpdateVisual();
                    SelectionChanged?.Invoke(this, _selectedIndex);
                }
            }
        }

        public string SelectedItem {
            get {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count) {
                    return _items[_selectedIndex];
                }
                return null;
            }
        }

        public bool IsDropDownOpen {
            get {
                return _isDropDownOpen;
            }
            set {
                _isDropDownOpen = value;
                UpdateDropDown();
            }
        }

        public float ItemHeight {
            get {
                return _itemHeight;
            }
            set {
                _itemHeight = value;
                UpdateVisual();
            }
        }

        public ComboBox(Scene scene) : base(scene) {
            BackgroundColor = new Color(45, 45, 45);
            Size = new Vector2(180f, 32f);

            _headerText = new TextElement(scene) {
                Text = "",
                ForegroundColor = Color.White,
                isMouseBypassEnabled = true
            };
            Children.Add(_headerText);

            _dropDownButton = new TextElement(scene) {
                Text = "?",
                ForegroundColor = Color.White,
                isMouseBypassEnabled = true
            };
            Children.Add(_dropDownButton);
        }

        private void UpdateVisual() {
            if (_selectedIndex >= 0 && _selectedIndex < _items.Count) {
                _headerText.Text = _items[_selectedIndex];
            } else {
                _headerText.Text = "";
            }

            _headerText.Position = new Vector2(Position.X + 8f, Position.Y + (Size.Y - _headerText.Size.Y) / 2f);
            _dropDownButton.Position = new Vector2(Position.X + Size.X - 24f, Position.Y + (Size.Y - _dropDownButton.Size.Y) / 2f);
        }

        private void UpdateDropDown() {
            // Remove existing dropdown items
            foreach (var item in _dropDownItems) {
                if (Scene != null) {
                    Scene.RemoveElement(item);
                }
            }
            _dropDownItems.Clear();

            if (_isDropDownOpen && Scene != null) {
                float yOffset = Position.Y + Size.Y;
                for (int i = 0, loopTo = _items.Count - 1; i <= loopTo; i++) {
                    int index = i;
                    var itemBg = new RectangleElement(Scene) {
                        Position = new Vector2(Position.X, yOffset),
                        Size = new Vector2(Size.X, _itemHeight),
                        BackgroundColor = i == _selectedIndex ? new Color(60, 100, 60) : new Color(55, 55, 55)
                    };

                    var itemText = new TextElement(Scene) {
                        Text = _items[i],
                        ForegroundColor = Color.White,
                        Position = new Vector2(Position.X + 8f, yOffset + 4f),
                        isMouseBypassEnabled = true
                    };

                    itemBg.Children.Add(itemText);
                    _dropDownItems.Add(itemBg);
                    Scene.AddElement(itemBg);

                    itemBg.MouseLeftClick += p => {
                        SelectedIndex = index;
                        IsDropDownOpen = false;
                    };

                    yOffset += _itemHeight;
                }
            }
        }

        public void AddItem(string item) {
            _items.Add(item);
            UpdateVisual();
        }

        public void RemoveItem(string item) {
            _items.Remove(item);
            if (_selectedIndex >= _items.Count) {
                _selectedIndex = _items.Count - 1;
            }
            UpdateVisual();
        }

        public void ClearItems() {
            _items.Clear();
            _selectedIndex = -1;
            UpdateVisual();
        }

        protected internal override void doDraw(GameTime gameTime) {
            base.doDraw(gameTime);
        }
    }

    /// <summary>
    /// A ListBox control that displays a scrollable list of items.
    /// </summary>
    public class ListBox : RectangleElement {

        private List<string> _items = new List<string>();
        private int _selectedIndex = -1;
        private List<RectangleElement> _itemElements = new List<RectangleElement>();
        private float _itemHeight = 24.0f;
        private float _scrollOffset = 0f;
        private Color _selectionColor = new Color(60, 100, 60);

        public event SelectionChangedEventHandler SelectionChanged;

        public delegate void SelectionChangedEventHandler(ListBox sender, int selectedIndex);

        public List<string> Items {
            get {
                return _items;
            }
            set {
                _items = value;
                RebuildItems();
            }
        }

        public int SelectedIndex {
            get {
                return _selectedIndex;
            }
            set {
                if (value >= -1 && value < _items.Count) {
                    _selectedIndex = value;
                    UpdateItemColors();
                    SelectionChanged?.Invoke(this, _selectedIndex);
                }
            }
        }

        public string SelectedItem {
            get {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count) {
                    return _items[_selectedIndex];
                }
                return null;
            }
        }

        public float ItemHeight {
            get {
                return _itemHeight;
            }
            set {
                _itemHeight = value;
                RebuildItems();
            }
        }

        public Color SelectionColor {
            get {
                return _selectionColor;
            }
            set {
                _selectionColor = value;
                UpdateItemColors();
            }
        }

        public ListBox(Scene scene) : base(scene) {
            BackgroundColor = new Color(35, 35, 35);
            Size = new Vector2(200f, 150f);
            Clip = true;
        }

        public void AddItem(string item) {
            _items.Add(item);
            RebuildItems();
        }

        public void RemoveItem(string item) {
            int index = _items.IndexOf(item);
            if (index >= 0) {
                _items.RemoveAt(index);
                if (_selectedIndex >= _items.Count) {
                    _selectedIndex = _items.Count - 1;
                }
                RebuildItems();
            }
        }

        public void ClearItems() {
            _items.Clear();
            _selectedIndex = -1;
            RebuildItems();
        }

        private void RebuildItems() {
            // Clear existing elements
            foreach (var elem in _itemElements)
                Children.Remove(elem);
            _itemElements.Clear();

            // Create new item elements
            float yOffset = 0f;
            for (int i = 0, loopTo = _items.Count - 1; i <= loopTo; i++) {
                int index = i;
                var itemBg = new RectangleElement(Scene) {
                    Position = new Vector2(0f, yOffset),
                    Size = new Vector2(Size.X, _itemHeight),
                    BackgroundColor = i == _selectedIndex ? _selectionColor : Color.Transparent
                };

                var itemText = new TextElement(Scene) {
                    Text = _items[i],
                    ForegroundColor = Color.White,
                    Position = new Vector2(6f, 2f),
                    isMouseBypassEnabled = true
                };

                itemBg.Children.Add(itemText);
                _itemElements.Add(itemBg);
                Children.Add(itemBg);

                itemBg.MouseLeftClick += p => SelectedIndex = index;

                yOffset += _itemHeight;
            }
        }

        private void UpdateItemColors() {
            for (int i = 0, loopTo = _itemElements.Count - 1; i <= loopTo; i++)
                _itemElements[i].BackgroundColor = i == _selectedIndex ? _selectionColor : Color.Transparent;
        }
    }

    /// <summary>
    /// A TabControl with multiple tabs.
    /// </summary>
    public class TabControl : RectangleElement {

        private List<TabItem> _tabs = new List<TabItem>();
        private int _selectedIndex = -1;
        private float _tabHeaderHeight = 32.0f;
        private List<Button> _tabHeaders = new List<Button>();
        private RectangleElement _contentArea;

        public event SelectionChangedEventHandler SelectionChanged;

        public delegate void SelectionChangedEventHandler(TabControl sender, int selectedIndex);

        public List<TabItem> Tabs {
            get {
                return _tabs;
            }
        }

        public int SelectedIndex {
            get {
                return _selectedIndex;
            }
            set {
                if (value >= 0 && value < _tabs.Count) {
                    _selectedIndex = value;
                    UpdateTabs();
                    SelectionChanged?.Invoke(this, _selectedIndex);
                }
            }
        }

        public float TabHeaderHeight {
            get {
                return _tabHeaderHeight;
            }
            set {
                _tabHeaderHeight = value;
                UpdateTabs();
            }
        }

        public TabControl(Scene scene) : base(scene) {
            BackgroundColor = new Color(40, 40, 40);
            Size = new Vector2(400f, 300f);

            _contentArea = new RectangleElement(scene) { BackgroundColor = new Color(30, 30, 30) };
            Children.Add(_contentArea);
        }

        public void AddTab(string header, SceneElement content) {
            var tab = new TabItem() {
                Header = header,
                Content = content
            };
            _tabs.Add(tab);

            if (_selectedIndex < 0) {
                _selectedIndex = 0;
            }

            UpdateTabs();
        }

        public void RemoveTab(int index) {
            if (index >= 0 && index < _tabs.Count) {
                _tabs.RemoveAt(index);
                if (_selectedIndex >= _tabs.Count) {
                    _selectedIndex = _tabs.Count - 1;
                }
                UpdateTabs();
            }
        }

        private void UpdateTabs() {
            // Clear existing headers
            foreach (var header in _tabHeaders)
                Children.Remove(header);
            _tabHeaders.Clear();

            // Clear content area children
            _contentArea.Children.Clear();

            if (_tabs.Count == 0)
                return;

            // Create tab headers
            float tabWidth = Size.X / _tabs.Count;
            for (int i = 0, loopTo = _tabs.Count - 1; i <= loopTo; i++) {
                int index = i;
                var header = new Button(Scene) {
                    Text = _tabs[i].Header,
                    Position = new Vector2(i * tabWidth, 0f),
                    Size = new Vector2(tabWidth, _tabHeaderHeight),
                    BackgroundColor = i == _selectedIndex ? new Color(50, 50, 50) : new Color(35, 35, 35)
                };

                header.MouseLeftClick += p => SelectedIndex = index;

                _tabHeaders.Add(header);
                Children.Add(header);
            }

            // Update content area
            _contentArea.Position = new Vector2(0f, _tabHeaderHeight);
            _contentArea.Size = new Vector2(Size.X, Size.Y - _tabHeaderHeight);

            // Add selected tab content
            if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count) {
                var content = _tabs[_selectedIndex].Content;
                if (content != null) {
                    _contentArea.Children.Add(content);
                }
            }
        }
    }

    /// <summary>
    /// Represents a single tab in a TabControl.
    /// </summary>
    public class TabItem {
        public string Header { get; set; }
        public SceneElement Content { get; set; }
    }

    /// <summary>
    /// A ScrollViewer control that provides scrolling for content larger than its viewport.
    /// </summary>
    public class ScrollViewer : RectangleElement {

        private SceneElement _content;
        private Vector2 _scrollOffset = Vector2.Zero;
        private RectangleElement _verticalScrollbar;
        private RectangleElement _horizontalScrollbar;
        private float _scrollbarWidth = 12.0f;
        private bool _canScrollVertically = true;
        private bool _canScrollHorizontally = false;

        public SceneElement Content {
            get {
                return _content;
            }
            set {
                if (_content != null) {
                    Children.Remove(_content);
                }
                _content = value;
                if (_content != null) {
                    Children.Add(_content);
                }
                UpdateScrollbars();
            }
        }

        public Vector2 ScrollOffset {
            get {
                return _scrollOffset;
            }
            set {
                _scrollOffset = value;
                UpdateContentPosition();
            }
        }

        public bool CanScrollVertically {
            get {
                return _canScrollVertically;
            }
            set {
                _canScrollVertically = value;
                UpdateScrollbars();
            }
        }

        public bool CanScrollHorizontally {
            get {
                return _canScrollHorizontally;
            }
            set {
                _canScrollHorizontally = value;
                UpdateScrollbars();
            }
        }

        public ScrollViewer(Scene scene) : base(scene) {
            BackgroundColor = new Color(30, 30, 30);
            Clip = true;

            _verticalScrollbar = new RectangleElement(scene) {
                BackgroundColor = new Color(60, 60, 60),
                isVisible = false
            };
            Children.Add(_verticalScrollbar);

            _horizontalScrollbar = new RectangleElement(scene) {
                BackgroundColor = new Color(60, 60, 60),
                isVisible = false
            };
            Children.Add(_horizontalScrollbar);
        }

        public void ScrollTo(Vector2 offset) {
            _scrollOffset = offset;
            ClampScrollOffset();
            UpdateContentPosition();
            UpdateScrollbars();
        }

        public void ScrollBy(Vector2 delta) {
            _scrollOffset += delta;
            ClampScrollOffset();
            UpdateContentPosition();
            UpdateScrollbars();
        }

        private void ClampScrollOffset() {
            if (_content is null) {
                _scrollOffset = Vector2.Zero;
                return;
            }

            float maxScrollX = Math.Max(0f, _content.Size.X - Size.X + (_canScrollVertically ? _scrollbarWidth : 0f));
            float maxScrollY = Math.Max(0f, _content.Size.Y - Size.Y + (_canScrollHorizontally ? _scrollbarWidth : 0f));

            _scrollOffset.X = Math.Max(0f, Math.Min(_scrollOffset.X, maxScrollX));
            _scrollOffset.Y = Math.Max(0f, Math.Min(_scrollOffset.Y, maxScrollY));
        }

        private void UpdateContentPosition() {
            if (_content != null) {
                _content.Position = -_scrollOffset;
            }
        }

        private void UpdateScrollbars() {
            if (_content is null) {
                _verticalScrollbar.isVisible = false;
                _horizontalScrollbar.isVisible = false;
                return;
            }

            float viewportWidth = Size.X - (_canScrollVertically ? _scrollbarWidth : 0f);
            float viewportHeight = Size.Y - (_canScrollHorizontally ? _scrollbarWidth : 0f);

            // Vertical scrollbar
            if (_canScrollVertically && _content.Size.Y > viewportHeight) {
                _verticalScrollbar.isVisible = true;
                float scrollRatio = viewportHeight / _content.Size.Y;
                float thumbHeight = Math.Max(20f, viewportHeight * scrollRatio);
                float thumbOffset = _scrollOffset.Y / (_content.Size.Y - viewportHeight) * (viewportHeight - thumbHeight);

                _verticalScrollbar.Position = new Vector2(Size.X - _scrollbarWidth, thumbOffset);
                _verticalScrollbar.Size = new Vector2(_scrollbarWidth, thumbHeight);
            } else {
                _verticalScrollbar.isVisible = false;
            }

            // Horizontal scrollbar
            if (_canScrollHorizontally && _content.Size.X > viewportWidth) {
                _horizontalScrollbar.isVisible = true;
                float scrollRatio = viewportWidth / _content.Size.X;
                float thumbWidth = Math.Max(20f, viewportWidth * scrollRatio);
                float thumbOffset = _scrollOffset.X / (_content.Size.X - viewportWidth) * (viewportWidth - thumbWidth);

                _horizontalScrollbar.Position = new Vector2(thumbOffset, Size.Y - _scrollbarWidth);
                _horizontalScrollbar.Size = new Vector2(thumbWidth, _scrollbarWidth);
            } else {
                _horizontalScrollbar.isVisible = false;
            }
        }
    }

    /// <summary>
    /// A Separator control - a simple horizontal or vertical line.
    /// </summary>
    public class Separator : RectangleElement {

        private Orientation _orientation = Orientation.Horizontal;

        public Orientation Orientation {
            get {
                return _orientation;
            }
            set {
                _orientation = value;
                UpdateSize();
            }
        }

        public Separator(Scene scene) : base(scene) {
            BackgroundColor = new Color(80, 80, 80);
            UpdateSize();
        }

        private void UpdateSize() {
            if (_orientation == Orientation.Horizontal) {
                Size = new Vector2(Size.X, 1f);
            } else {
                Size = new Vector2(1f, Size.Y);
            }
        }
    }

    /// <summary>
    /// An Expander control that can show/hide content.
    /// </summary>
    public class Expander : RectangleElement {

        private string _header = "Expander";
        private bool _isExpanded = true;
        private SceneElement _content;
        private Button _headerButton;
        private RectangleElement _contentContainer;
        private float _headerHeight = 28.0f;

        public event ExpandedChangedEventHandler ExpandedChanged;

        public delegate void ExpandedChangedEventHandler(Expander sender, bool isExpanded);

        public string Header {
            get {
                return _header;
            }
            set {
                _header = value;
                if (_headerButton != null) {
                    _headerButton.Text = (_isExpanded ? "? " : "? ") + _header;
                }
            }
        }

        public bool IsExpanded {
            get {
                return _isExpanded;
            }
            set {
                _isExpanded = value;
                UpdateExpansion();
                ExpandedChanged?.Invoke(this, _isExpanded);
            }
        }

        public SceneElement Content {
            get {
                return _content;
            }
            set {
                if (_content != null) {
                    _contentContainer.Children.Remove(_content);
                }
                _content = value;
                if (_content != null) {
                    _contentContainer.Children.Add(_content);
                }
                UpdateExpansion();
            }
        }

        public float HeaderHeight {
            get {
                return _headerHeight;
            }
            set {
                _headerHeight = value;
                UpdateLayout();
            }
        }

        public Expander(Scene scene) : base(scene) {
            BackgroundColor = new Color(40, 40, 40);
            Size = new Vector2(200f, 150f);

            _headerButton = new Button(scene) {
                Text = "? " + _header,
                Position = new Vector2(0f, 0f),
                Size = new Vector2(Size.X, _headerHeight),
                BackgroundColor = new Color(50, 50, 50)
            };
            Children.Add(_headerButton);

            _headerButton.MouseLeftClick += p => IsExpanded = !IsExpanded;

            _contentContainer = new RectangleElement(scene) {
                BackgroundColor = Color.Transparent,
                Position = new Vector2(0f, _headerHeight)
            };
            Children.Add(_contentContainer);

            UpdateExpansion();
        }

        private void UpdateExpansion() {
            if (_headerButton != null) {
                _headerButton.Text = (_isExpanded ? "? " : "? ") + _header;
            }

            if (_contentContainer != null) {
                _contentContainer.isVisible = _isExpanded;
            }

            UpdateLayout();
        }

        private void UpdateLayout() {
            if (_headerButton != null) {
                _headerButton.Size = new Vector2(Size.X, _headerHeight);
            }

            if (_contentContainer != null) {
                _contentContainer.Position = new Vector2(0f, _headerHeight);
                _contentContainer.Size = new Vector2(Size.X, Size.Y - _headerHeight);
            }

            if (!_isExpanded) {
                Size = new Vector2(Size.X, _headerHeight);
            }
        }
    }

    /// <summary>
    /// A ToolTip control that can be attached to elements.
    /// </summary>
    public class ToolTip : RectangleElement {

        private string _content = "";
        private TextElement _textElement;
        private float _showDelay = 0.5f;
        private float _hideDelay = 5.0f;

        public string Content {
            get {
                return _content;
            }
            set {
                _content = value;
                if (_textElement != null) {
                    _textElement.Text = _content;
                }
                UpdateSize();
            }
        }

        public float ShowDelay {
            get {
                return _showDelay;
            }
            set {
                _showDelay = value;
            }
        }

        public float HideDelay {
            get {
                return _hideDelay;
            }
            set {
                _hideDelay = value;
            }
        }

        public ToolTip(Scene scene) : base(scene) {
            BackgroundColor = new Color(30, 30, 30, 240);
            isVisible = false;
            isMouseBypassEnabled = true;

            _textElement = new TextElement(scene) {
                Text = "",
                ForegroundColor = Color.White,
                Position = new Vector2(6f, 4f),
                isMouseBypassEnabled = true
            };
            Children.Add(_textElement);
        }

        public void Show(Vector2 position) {
            Position = position;
            isVisible = true;
        }

        public void Hide() {
            isVisible = false;
        }

        private void UpdateSize() {
            if (_textElement != null) {
                Size = new Vector2(_textElement.Size.X + 12f, _textElement.Size.Y + 8f);
            }
        }
    }

    /// <summary>
    /// A GroupBox control with a header border.
    /// </summary>
    public class GroupBox : RectangleElement {

        private string _header = "Group";
        private TextElement _headerText;
        private RectangleElement _contentArea;
        private float _headerHeight = 20.0f;
        private Color _borderColor = new Color(80, 80, 80);

        public string Header {
            get {
                return _header;
            }
            set {
                _header = value;
                if (_headerText != null) {
                    _headerText.Text = _header;
                }
            }
        }

        public SceneElement Content {
            get {
                if (_contentArea.Children.Count > 0) {
                    return _contentArea.Children[0];
                }
                return null;
            }
            set {
                _contentArea.Children.Clear();
                if (value != null) {
                    _contentArea.Children.Add(value);
                }
            }
        }

        public Color BorderColor {
            get {
                return _borderColor;
            }
            set {
                _borderColor = value;
            }
        }

        public GroupBox(Scene scene) : base(scene) {
            BackgroundColor = new Color(35, 35, 35);
            Size = new Vector2(200f, 150f);

            _headerText = new TextElement(scene) {
                Text = _header,
                ForegroundColor = Color.LightGray,
                Position = new Vector2(10f, 2f),
                isMouseBypassEnabled = true
            };
            Children.Add(_headerText);

            _contentArea = new RectangleElement(scene) {
                BackgroundColor = Color.Transparent,
                Position = new Vector2(4f, _headerHeight),
                Size = new Vector2(Size.X - 8f, Size.Y - _headerHeight - 4f)
            };
            Children.Add(_contentArea);
        }
    }

    /// <summary>
    /// A NumericUpDown control for number input with increment/decrement buttons.
    /// </summary>
    public class NumericUpDown : RectangleElement {

        private double _value = 0d;
        private double _minimum = 0d;
        private double _maximum = 100d;
        private double _increment = 1d;
        private TextElement _valueText;
        private Button _upButton;
        private Button _downButton;

        public event ValueChangedEventHandler ValueChanged;

        public delegate void ValueChangedEventHandler(NumericUpDown sender, double value);

        public double Value {
            get {
                return _value;
            }
            set {
                double newValue = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != newValue) {
                    _value = newValue;
                    UpdateDisplay();
                    ValueChanged?.Invoke(this, _value);
                }
            }
        }

        public double Minimum {
            get {
                return _minimum;
            }
            set {
                _minimum = value;
                if (_value < _minimum)
                    Value = _minimum;
            }
        }

        public double Maximum {
            get {
                return _maximum;
            }
            set {
                _maximum = value;
                if (_value > _maximum)
                    Value = _maximum;
            }
        }

        public double Increment {
            get {
                return _increment;
            }
            set {
                _increment = value;
            }
        }

        public NumericUpDown(Scene scene) : base(scene) {
            BackgroundColor = new Color(45, 45, 45);
            Size = new Vector2(100f, 28f);

            float buttonWidth = 24f;

            _valueText = new TextElement(scene) {
                Text = "0",
                ForegroundColor = Color.White,
                Position = new Vector2(4f, 4f),
                isMouseBypassEnabled = true
            };
            Children.Add(_valueText);

            _upButton = new Button(scene) {
                Text = "?",
                Position = new Vector2(Size.X - buttonWidth, 0f),
                Size = new Vector2(buttonWidth, Size.Y / 2f),
                BackgroundColor = new Color(60, 60, 60)
            };
            Children.Add(_upButton);

            _downButton = new Button(scene) {
                Text = "?",
                Position = new Vector2(Size.X - buttonWidth, Size.Y / 2f),
                Size = new Vector2(buttonWidth, Size.Y / 2f),
                BackgroundColor = new Color(60, 60, 60)
            };
            Children.Add(_downButton);

            _upButton.MouseLeftClick += p => Value += _increment;
            _downButton.MouseLeftClick += p => Value -= _increment;

            UpdateDisplay();
        }

        private void UpdateDisplay() {
            if (_valueText != null) {
                _valueText.Text = _value.ToString("F2");
            }
        }
    }

}