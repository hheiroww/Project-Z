using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Drawing.UI.Advanced;
using ProjectZ.Shared.Drawing.UI.Input;
using SocketJack;
using SocketJack.Serialization;
using System;
using System.Runtime.CompilerServices;

namespace ProjectZ.Shared.Drawing.UI {

    [Serializable]
    public abstract class SceneElement : IDisposable {

        #region Properties

        public bool isUserInvalidated = false;

        public bool isLoaded { get; set; } = false;

        public bool isPrototype { get; set; } = false;

        public bool isManagedSpritebatch {
            get {
                return _isManagedSpritebatch;
            }
        }
        private bool _isManagedSpritebatch = false;

        public bool ForceSpriteBatchBegin { get; set; } = false;

        protected internal ProjectZ.Shared.XNA.SpriteBatchWrapper spriteBatch;

        public Vector2 Position {
            get {
                return _Position;
            }
            set {
                var OldPosition = _Position;
                _Position = value;
                PositionChanged?.Invoke(OldPosition, _Position);
            }
        }
        private Vector2 _Position;

        public Vector2 Size {
            get {
                return _Size;
            }
            set {
                var oldSize = _Size;
                _Size = value;
                SizeChanged?.Invoke(oldSize, _Size);
            }
        }
        private Vector2 _Size;

        public Thickness Padding {
            get {
                return _Padding;
            }
            set {
                _Padding = value;
                _Valid = false;
            }
        }
        private Thickness _Padding = new Thickness(3);

        // WPF-like minimum/maximum size
        public Vector2 MinSize {
            get {
                return _MinSize;
            }
            set {
                _MinSize = value;
            }
        }
        private Vector2 _MinSize = Vector2.Zero;

        public Vector2 MaxSize {
            get {
                return _MaxSize;
            }
            set {
                _MaxSize = value;
            }
        }
        private Vector2 _MaxSize = new Vector2(float.MaxValue, float.MaxValue);

        // For stretch alignment support
        public bool StretchToParent { get; set; } = false;

        public Thickness Margin {
            get {
                return _Margin;
            }
            set {
                _Margin = value;
                _Valid = false;
            }
        }
        private Thickness _Margin = new Thickness(0);

        public int zIndex {
            get {
                return _zIndex;
            }
            set {
                _zIndex = value;
                IndexChanged?.Invoke();
            }
        }
        private int _zIndex = 0;

        public bool isEnabled { get; set; } = true;

        public bool isSelected {
            get {
                return _isSelected;
            }
            set {
                if (CanSelect) {
                    _isSelected = value;
                    switch (_isSelected) {
                        case true: {
                                Selected?.Invoke();
                                break;
                            }
                        case false: {
                                Deselected?.Invoke();
                                break;
                            }
                    }
                }
            }
        }
        private bool _isSelected = false;

        public bool CanSelect { get; set; } = false;
        public bool isMouseOver {
            get {
                return _MouseOver;
            }
            set {
                _MouseOver = value;
                switch (_MouseOver) {
                    case true: {
                            MouseEnter?.Invoke();
                            break;
                        }
                    case false: {
                            MouseLeave?.Invoke();
                            break;
                        }
                }
            }
        }
        private bool _MouseOver = false;

        public bool isMouseDown {
            get {
                return _MouseDown;
            }
        }
        private bool _MouseDown = false;
        private Vector2 _MouseDownPosition;

        public bool isMovable {
            get {
                return _isMovable;
            }
            set {
                _isMovable = value;
            }
        }
        private bool _isMovable = false;

        public bool isMouseBypassEnabled {
            get {
                return _isMouseBypassEnabled;
            }
            set {
                _isMouseBypassEnabled = value;
            }
        }
        private bool _isMouseBypassEnabled = false;

        protected bool isElementSpriteBatch {
            get {
                return _isElementSpriteBatch;
            }
            set {
                _isElementSpriteBatch = value;
            }
        }
        private bool _isElementSpriteBatch;


        private ChildCollection __Children;

        internal virtual ChildCollection _Children {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get {
                return __Children;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set {
                if (__Children != null) {
                    __Children.ChildAdded -= _Children_ChildAdded;
                    __Children.ChildRemoved -= _Children_ChildRemoved;
                }

                __Children = value;
                if (__Children != null) {
                    __Children.ChildAdded += _Children_ChildAdded;
                    __Children.ChildRemoved += _Children_ChildRemoved;
                }
            }
        }

        public ChildCollection Children {
            get {
                return _Children;
            }
        }


        public HorizontalAlignment HorizontalAlign {
            get {
                return _HorizontalAlign;
            }
            set {
                _HorizontalAlign = value;
                AlignmentChanged?.Invoke(_HorizontalAlign, _VerticalAlign);
            }
        }
        private HorizontalAlignment _HorizontalAlign = HorizontalAlignment.Left;

        public VerticalAlignment VerticalAlign {
            get {
                return _VerticalAlign;
            }
            set {
                _VerticalAlign = value;
                AlignmentChanged?.Invoke(_HorizontalAlign, _VerticalAlign);
            }
        }
        private VerticalAlignment _VerticalAlign = VerticalAlignment.Top;

        public DisplayReservation OrientationReserve { get; set; } = DisplayReservation.FloatBoth;

        private bool Valid {
            get {
                return _Valid;
            }
            set {
                _Valid = value;
                if (!_Valid)
                    Invalidated?.Invoke(this);
            }
        }
        private bool _Valid = false;
        private bool _isValidating = false;
        private bool _isCheckingValidation = false;

        public bool isValidated() {
            return Valid;
        }

        public Rectangle Rectangle {
            get {
                return _Rectangle;
            }
            set {
                _Rectangle = value;
            }
        }
        private Rectangle _Rectangle = new Rectangle(0, 0, 0, 0);

        public bool isVisible { get; set; } = true;

        public string GUID { get; set; } = "";


        internal Scene Scene;

        protected internal SceneElement Parent;

        public bool Clip { get; set; } = false;

        #endregion

        #region Events

        public event RectangleChangedEventHandler RectangleChanged;

        public delegate void RectangleChangedEventHandler();
        public event SelectedEventHandler Selected;

        public delegate void SelectedEventHandler();
        public event DeselectedEventHandler Deselected;

        public delegate void DeselectedEventHandler();
        public event PositionChangedEventHandler PositionChanged;

        public delegate void PositionChangedEventHandler(Vector2 oldPosition, Vector2 newPosition);
        public event SizeChangedEventHandler SizeChanged;

        public delegate void SizeChangedEventHandler(Vector2 oldSize, Vector2 newSize);
        public event IndexChangedEventHandler IndexChanged;

        public delegate void IndexChangedEventHandler();
        public event DrawFinishedEventHandler DrawFinished;

        public delegate void DrawFinishedEventHandler(TimeSpan DrawTime);
        public event PreDrawEventHandler PreDraw;

        public delegate void PreDrawEventHandler(GameTime gameTime);
        public event AlignmentChangedEventHandler AlignmentChanged;

        public delegate void AlignmentChangedEventHandler(HorizontalAlignment HorizontalAlignment, VerticalAlignment VerticalAlignment);
        public event PreviewInvalidatedEventHandler PreviewInvalidated;

        public delegate void PreviewInvalidatedEventHandler(SceneElement sender);
        public event InvalidatedEventHandler Invalidated;

        public delegate void InvalidatedEventHandler(SceneElement sender);
        public event UserInvalidatedEventHandler UserInvalidated;

        public delegate void UserInvalidatedEventHandler(SceneElement sender);
        public event LoadedEventHandler Loaded;

        public delegate void LoadedEventHandler();

        #region Child Events

        private void _Children_ChildAdded(SceneElement c) {
            c.Invalidated += OnPreviewInvalidated;
            if (Scene.Elements.Contains(this)) {
                Scene.AddElement(c);
            }
        }

        private void _Children_ChildRemoved(SceneElement c) {
            c.Invalidated -= OnPreviewInvalidated;
            if (Scene.Elements.Contains(this)) {
                Scene.RemoveElement(c);
            }
        }

        protected internal void OnPreviewInvalidated(SceneElement sender) {
            PreviewInvalidated?.Invoke(sender);
            ValidationCheck();
        }

        public void OnUserInvalidated() {
            UserInvalidated?.Invoke(this);
        }

        #endregion

        #region Keyboard Events

        public event OnKeyPressEventHandler OnKeyPress;

        public delegate void OnKeyPressEventHandler(Keys Key, KeyboardState KeyboardState);
        public event OnKeyDownEventHandler OnKeyDown;

        public delegate void OnKeyDownEventHandler(Keys Key, KeyboardState KeyboardState);
        public event OnKeyUpEventHandler OnKeyUp;

        public delegate void OnKeyUpEventHandler(Keys Key, KeyboardState KeyboardState);

        protected internal void KeyPress(Keys Key, KeyboardState KeyboardState) {
            OnKeyPress?.Invoke(Key, KeyboardState);
        }

        protected internal void KeyDown(Keys Key, KeyboardState KeyboardState) {
            OnKeyDown?.Invoke(Key, KeyboardState);
        }

        protected internal void KeyUp(Keys Key, KeyboardState KeyboardState) {
            OnKeyUp?.Invoke(Key, KeyboardState);
        }

        #endregion

        #region Mouse Events
        public event MouseMoveEventHandler MouseMove;

        public delegate void MouseMoveEventHandler(Point currentPoint, Point lastPoint);
        public event MouseDragEventHandler MouseDrag;

        public delegate void MouseDragEventHandler(Point currentPoint, Point startPoint);
        public event MouseEnterEventHandler MouseEnter;

        public delegate void MouseEnterEventHandler();
        public event MouseLeaveEventHandler MouseLeave;

        public delegate void MouseLeaveEventHandler();
        public event MouseRightClickEventHandler MouseRightClick;

        public delegate void MouseRightClickEventHandler(Point p);
        public event MouseLeftClickEventHandler MouseLeftClick;

        public delegate void MouseLeftClickEventHandler(Point p);
        public event MouseLeftDownEventHandler MouseLeftDown;

        public delegate void MouseLeftDownEventHandler(Point p);
        public event MouseLeftUpEventHandler MouseLeftUp;

        public delegate void MouseLeftUpEventHandler(Point p);
        public event DragDropEventHandler DragDrop;

        public delegate void DragDropEventHandler(Point p, SceneElement Element);
        public event DragOverEventHandler DragOver;

        public delegate void DragOverEventHandler(Point p, SceneElement Element);

        protected internal void OnMouseLeftDown(Point p) {
            _MouseDown = true;
            _MouseDownPosition = Position;
            MouseLeftDown?.Invoke(p);
        }

        protected internal void OnMouseLeftUp(Point p) {
            _MouseDown = false;
            MouseLeftUp?.Invoke(p);
        }

        protected internal void OnMouseLeftClick(Point p) {
            MouseLeftClick?.Invoke(p);
        }

        protected internal void OnMouseRightClick(Point p) {
            MouseRightClick?.Invoke(p);
        }

        protected internal void OnMouseMove(Point currentPoint, Point lastPoint) {
            MouseMove?.Invoke(currentPoint, lastPoint);
        }

        protected internal void OnMouseDrag(Point currentPoint, Point startPoint) {
            MouseDrag?.Invoke(currentPoint, startPoint);
            if (isMovable)
            {
                // Move by the delta between currentPoint and startPoint
                var delta = new Vector2(currentPoint.X - startPoint.X, currentPoint.Y - startPoint.Y);
                Position = _MouseDownPosition + delta;
            }
        }

        protected internal void OnMouseEnter() {
            MouseEnter?.Invoke();
        }

        protected internal void OnMouseLeave() {
            MouseLeave?.Invoke();
        }

        protected internal void OnMouseDragOver(Point p, ref SceneElement Element) {
            DragOver?.Invoke(p, Element);
        }

        protected internal void OnMouseDragDrop(Point p, ref SceneElement Element) {
            DragDrop?.Invoke(p, Element);
        }

        #endregion

        protected internal void OnLoaded() {
            Loaded?.Invoke();
        }

        protected internal void OnDrawFinished(TimeSpan TimeSpan) {
            DrawFinished?.Invoke(TimeSpan);
        }

        protected internal void OnPreDraw(GameTime gameTime) {
            PreDraw?.Invoke(gameTime);
        }

        protected internal void OnSizeChanged(Vector2 oldSize, Vector2 newSize) {
            SizeChanged?.Invoke(oldSize, newSize);
        }

        protected internal void OnPositionChanged(Vector2 oldPosition, Vector2 newPosition) {
            PositionChanged?.Invoke(oldPosition, newPosition);
        }

        protected internal void OnInvalidated(SceneElement sender) {
            Invalidated?.Invoke(sender);
        }

        #endregion

        #region Animation Properties

        internal ProjectZ.Shared.Animations.Properties.LeftProperty LeftProperty;

        class _failedMemberConversionMarker1 {
        }


        internal ProjectZ.Shared.Animations.Properties.TopProperty TopProperty;
        internal ProjectZ.Shared.Animations.Properties.LeftProperty WidthProperty;

        class _failedMemberConversionMarker2 {
        }

        #endregion

        #region Container Functionality

        public void ValidationCheck() {
            if (_isCheckingValidation)
                return;
            _isCheckingValidation = true;
            try {
                if (!Valid)
                    Validate();
                Children.ForEach(c => { if (c != null) c.ValidationCheck(); });
            } finally {
                _isCheckingValidation = false;
            }
        }

        public void Invalidate() {
            Valid = false;
            Validate();
        }

        private void Validate() {
            if (_isValidating)
                return;
            _isValidating = true;
            try {
                AlignChildren();
                Valid = true;
            } finally {
                _isValidating = false;
            }
        }

        private bool HorizontalReserved() {
            return OrientationReserve == DisplayReservation.ReserveBoth | OrientationReserve == DisplayReservation.ReserveX;
        }

        private bool VerticalReserved() {
            return OrientationReserve == DisplayReservation.ReserveBoth | OrientationReserve == DisplayReservation.ReserveY;
        }

        private bool isFloat() {
            return OrientationReserve == DisplayReservation.FloatBoth | OrientationReserve == DisplayReservation.FloatX | OrientationReserve == DisplayReservation.FloatY;
        }

        private void AlignChildren() {
            // Order, Size, and Position Children
            var CurrentPosition = new Vector2(Padding.Left, Padding.Top);
            Children.ForEach(c => AlignChild(c, ref CurrentPosition));
        }

        private void AlignChild(SceneElement c, ref Vector2 CurrentPosition) {
            if (c is null)
                return;
            if (c.Parent is null)
                return;

            var newPosition = new Vector2(c.Parent.Position.X, c.Parent.Position.Y);
            var newSize = new Vector2(c.Size.X, c.Size.Y);

            bool hReserved = c.HorizontalReserved();
            bool vReserved = c.VerticalReserved();

            newPosition.X += CurrentPosition.X;
            newPosition.Y += CurrentPosition.Y;

            if (hReserved)
                CurrentPosition.X += c.Size.X; // + (c.Parent.Padding.Left + c.Parent.Padding.Right)
            if (vReserved)
                CurrentPosition.Y += c.Size.Y; // + (c.Parent.Padding.Top + c.Parent.Padding.Bottom)
            switch (c.HorizontalAlign) {
                case HorizontalAlignment.Stretch: {
                        newSize.X = c.Parent.Size.X - (c.Parent.Padding.Left + c.Parent.Padding.Right);
                        break;
                    }
                case HorizontalAlignment.Right: {
                        newPosition.X += Size.X - (c.Size.X + c.Parent.Padding.Right);
                        break;
                    }
                case HorizontalAlignment.Center: {
                        newPosition.X += c.Parent.Size.X / 2f - c.Size.X / 2f;
                        newPosition.X -= (float)((c.Parent.Padding.Left + c.Parent.Padding.Right) / 2d);
                        break;
                    }

                default: {
                        newPosition.X += c.Margin.Left;
                        break;
                    }
            }


            switch (c.VerticalAlign) {
                case VerticalAlignment.Stretch: {
                        newSize.Y = c.Parent.Size.Y - (c.Parent.Padding.Top + Padding.Bottom);
                        break;
                    }
                case VerticalAlignment.Bottom: {
                        newPosition.Y += c.Parent.Size.Y - (c.Size.Y + c.Parent.Padding.Bottom);
                        break;
                    }
                case VerticalAlignment.Center: {
                        newPosition.Y += c.Parent.Size.Y / 2f - c.Size.Y / 2f;
                        newPosition.Y -= (float)((c.Parent.Padding.Top + c.Parent.Padding.Bottom) / 2d);
                        break;
                    }

                default: {
                        newPosition.Y += c.Margin.Top;
                        break;
                    }
            }

            if (c.Position != newPosition) {
                c.Position = newPosition;
            }

            if (c.Size != newSize) {
                c.Size = newSize;
            }

            c.Valid = true;

        }

        protected internal Action ClickActionInstance;

        public void SetClickAction(Action action) {
            ClickActionInstance = action;
        }

        public bool clickActionExists() {
            return ClickActionInstance != null;
        }

        /// <summary>
        /// Brings this element to the front of the scene's rendering order.
        /// The element will be removed and re-added to the scene with the highest z-index.
        /// </summary>
        public void BringToFront() {
            if (Scene is null)
                return;
            if (!Scene.ContainsElement(this))
                return;
            Scene.RemoveElement(this);
            Scene.AddElement(this);
        }

        /// <summary>
        /// Sends this element to the back of the scene's rendering order.
        /// Note: This is implemented by removing and re-adding with the lowest z-index.
        /// </summary>
        public void SendToBack() {
            if (Scene is null)
                return;
            if (!Scene.ContainsElement(this))
                return;
            // Set zIndex to minimum before removing so it gets placed at back when re-added
            // The Scene.AddElement will assign a new index, but we set it negative first
            zIndex = int.MinValue;
            Scene.RemoveElement(this);
            Scene.AddElement(this);
        }
        #endregion

        #region Prototyping

        private PrototypeElement ProtoButton;

        public void UpdateElement(SceneElement newElement) {
            var PropertyList = Wrapper.GetPropertyReferences(this);
            var NewPropertyList = Wrapper.GetPropertyReferences(newElement);
            for (int i = 0, loopTo = PropertyList.Count - 1; i <= loopTo; i++) {
                var p = PropertyList[i];
                var np = NewPropertyList[i];
                if (p.Info.CanWrite) {
                    if ((p.Info.Name ?? "") == (np.Info.Name ?? "")) {
                        var v = np.Info.GetValue(newElement);
                        var ov = np.Info.GetValue(newElement);
                        var oldElement = this;
                        // If v.GetType = GetType(Integer) Then
                        // Dim ianim As New DoubleAnimation(New Easing.CircleEase(Easing.EaseType.EaseInOut), CDbl(ov), CDbl(v), TimeSpan.FromMilliseconds(16.7), Scene.gameTime)
                        // oldElement.BindAnimation(oldElement.HeightProperty, ianim)
                        // oldElement.BindAnimation(oldElement.WidthProperty, ianim)
                        // ElseIf v.GetType = GetType(Double) Then
                        // End If
                        p.Info.SetValue(this, v);

                    }
                }

            }
        }

        #endregion

        private ProjectZ.Shared.Animations.Timeline Timeline;

        protected internal abstract void Draw(GameTime gameTime);

        protected internal virtual bool ContainsPoint(Point p) {
            return Rectangle.Contains(p);
        }

        protected internal virtual void doDraw(GameTime gameTime) {
            try {
                spriteBatch.Begin();
                Draw(gameTime);
                spriteBatch.End();
            } catch (Exception ex) {

            }
        }

        public virtual void Tick(GameTime gameTime) {
            try {
                Timeline.Tick(gameTime);
            } catch (Exception ex) {

            }
        }

        public void updateScene(Scene Scene) {
            if (this.Scene is null) {
                this.Scene = Scene;
                Timeline = new ProjectZ.Shared.Animations.Timeline(Scene.gameTime);
                if (spriteBatch is null) {
                    spriteBatch = new ProjectZ.Shared.XNA.SpriteBatchWrapper(Scene.graphicsDevice);
                    isElementSpriteBatch = true;
                }
            }
        }

        public bool isBindedTo(ProjectZ.Shared.Animations.AnimationBase Animation) {
            return Timeline.isChild(Animation);
        }

        public void BindAnimation(ProjectZ.Shared.Animations.Properties.ElementProperty TargetProperty, ProjectZ.Shared.Animations.AnimationBase Animation) {
            Timeline.AddChild(Animation, TargetProperty);
        }

        public void UnbindAnimation(ProjectZ.Shared.Animations.AnimationBase Animation) {
            Timeline.RemoveChild(Animation);
        }

        private void SetRectangleProperty() {
            var oldRect = Rectangle;
            _Rectangle = new Rectangle((int)Math.Round(Position.X), (int)Math.Round(Position.Y), (int)Math.Round(Size.X), (int)Math.Round(Size.Y));
            RectangleChanged?.Invoke();
            if (oldRect != _Rectangle)
                Valid = false;
        }

        public Point RelativeTo(SceneElement Element) {
            return new Point((int)Math.Round(Position.X + (Position.X - Element.Position.X)), (int)Math.Round(Position.Y + (Position.Y - Element.Position.Y)));
        }

        public SceneElement() {
            _Children = new ChildCollection(this);
            LeftProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            TopProperty = new ProjectZ.Shared.Animations.Properties.TopProperty(this);
            WidthProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            SizeChanged += (_, __) => SetRectangleProperty();
            PositionChanged += (_, __) => SetRectangleProperty();
            AlignmentChanged += (_, __) => SetRectangleProperty();
            DrawFinished += SceneElement_DrawFinished;
            Loaded += SceneElement_Loaded;

        }

        public SceneElement(Scene Scene) {
            _Children = new ChildCollection(this);
            LeftProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            WidthProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            this.Scene = Scene;
            GUID = Guid.NewGuid().ToString();
            Timeline = new ProjectZ.Shared.Animations.Timeline(Scene.gameTime);
            if (spriteBatch is null) {
                spriteBatch = new ProjectZ.Shared.XNA.SpriteBatchWrapper(Scene.graphicsDevice);
                isElementSpriteBatch = true;
            }

            SizeChanged += (_, __) => SetRectangleProperty();
            PositionChanged += (_, __) => SetRectangleProperty();
            AlignmentChanged += (_, __) => SetRectangleProperty();
            DrawFinished += SceneElement_DrawFinished;
            Loaded += SceneElement_Loaded;
        }

        public SceneElement(Scene Scene, bool newSpritebatch) {
            _Children = new ChildCollection(this);
            LeftProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            TopProperty = new ProjectZ.Shared.Animations.Properties.TopProperty();
            WidthProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            this.Scene = Scene;
            GUID = Guid.NewGuid().ToString();
            Timeline = new ProjectZ.Shared.Animations.Timeline(Scene.gameTime);
            if (newSpritebatch) {
                spriteBatch = new ProjectZ.Shared.XNA.SpriteBatchWrapper(Scene.graphicsDevice);
                isElementSpriteBatch = true;
            } else {
                spriteBatch = new ProjectZ.Shared.XNA.SpriteBatchWrapper(Scene.GetSpriteBatch());
                _isManagedSpritebatch = true;
                isElementSpriteBatch = false;
            }

            SizeChanged += (_, __) => SetRectangleProperty();
            PositionChanged += (_, __) => SetRectangleProperty();
            AlignmentChanged += (_, __) => SetRectangleProperty();
            DrawFinished += SceneElement_DrawFinished;
            Loaded += SceneElement_Loaded;
        }

        public SceneElement(Scene Scene, ref SpriteBatch spriteBatch) {
            _Children = new ChildCollection(this);
            LeftProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            TopProperty = new ProjectZ.Shared.Animations.Properties.TopProperty();
            WidthProperty = new ProjectZ.Shared.Animations.Properties.LeftProperty(this);
            this.Scene = Scene;
            GUID = Guid.NewGuid().ToString();
            Timeline = new ProjectZ.Shared.Animations.Timeline(Scene.gameTime);
            this.spriteBatch = new ProjectZ.Shared.XNA.SpriteBatchWrapper(spriteBatch);
            isElementSpriteBatch = false;
            _isManagedSpritebatch = true;
            SizeChanged += (_, __) => SetRectangleProperty();
            PositionChanged += (_, __) => SetRectangleProperty();
            AlignmentChanged += (_, __) => SetRectangleProperty();
            DrawFinished += SceneElement_DrawFinished;
            Loaded += SceneElement_Loaded;
        }

        private void @init() {
            if (!isPrototype && !GetType().IsSubclassOf(typeof(PrototypeElement))) {
                ProtoButton = new PrototypeElement(Scene, this);
                {
                    ref var withBlock = ref ProtoButton;
                    withBlock.AutoSize = ButtonAutoSize.X;
                    withBlock.Size = new Vector2(withBlock.Size.X, 18f);
                    withBlock.HorizontalAlign = HorizontalAlignment.Right;
                    withBlock.VerticalAlign = VerticalAlignment.Top;
                    withBlock.Text = "-";
                }
                Children.Add(ProtoButton);
            }
        }

        private void SceneElement_DrawFinished(TimeSpan DrawTime) {
            if (!isLoaded) {
                isLoaded = true;
                Loaded?.Invoke();
            } else {
                // ProtoButton.doDraw(Scene.gameTime)
                // ValidationCheck()
            }
        }

        private void SceneElement_Loaded() {
            @init();
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // Dispose managed resources
                    // Dispose children first
                    foreach (var child in _Children)
                        if (child != null) child.Dispose();
                    _Children.Clear();

                    // Dispose the spriteBatch wrapper if we own it
                    if (isElementSpriteBatch && spriteBatch != null) {
                        spriteBatch.Dispose();
                        spriteBatch = (ProjectZ.Shared.XNA.SpriteBatchWrapper)null;
                    }

                    // Clear references to prevent memory leaks
                    Timeline = (ProjectZ.Shared.Animations.Timeline)null;
                    ClickActionInstance = null;
                }
            }
            disposedValue = true;
        }

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }

    public enum AlignmentType {
        Horizontal,
        Vertical,
        None
    }

    public enum HorizontalAlignment {
        Left,
        Center,
        Right,
        Stretch
    }

    public enum VerticalAlignment {
        Top,
        Center,
        Bottom,
        Stretch
    }

    public enum DisplayReservation {
        FloatBoth,
        FloatX,
        FloatY,
        ReserveBoth,
        ReserveX,
        ReserveY
    }

}