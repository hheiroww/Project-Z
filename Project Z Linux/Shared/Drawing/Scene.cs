using System;
#region Using Statements
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Advanced;
using ProjectZ.Shared.Extensions;

#endregion

namespace ProjectZ.Shared.Drawing {

    public abstract class Scene : IDisposable {

        #region Properties

        internal RenderTarget2D renderTarget;

        public Game sender() {
            return _sender;
        }
        private Game _sender;
        public MouseState MouseState {
            get {
                return _MouseState;
            }
        }

        private MouseState _MouseState;
        public ProjectZ.Shared.XNA.SpriteBatchPropertySet SpriteSettings { get; set; }
        public bool isInitialized = false;
        public bool isCursorVisible { get; set; } = false;
        public bool UseRenderTarget { get; set; } = false;
        public Color BackgroundColor { get; set; } = new Color(0, 0, 0, 0);
        public List<Effect> Effects;

        public Point MousePosition {
            get {
                return _MouseState.Position;
            }
        }

        public int FPS = 60;
        public int DrawFPS = 60;

        public IList<SceneElement> Elements {
            get {
                return I_Elements.Values;
            }
        }

        #endregion

        #region Internals

        protected internal bool hasBegun = false;

        protected internal GameTime gameTime = new GameTime();

        #region Constants

        private const long OneSecond = 10000000L;

        #endregion

        protected internal ProjectZ.Shared.XNA.SpriteBatchPropertySet RenderTargetOptions;

        protected internal Texture2D Texture;

        private long LastTick = 0L;

        private SceneElement LastSelected;

        private SceneElement LastMouseDrag;

        public PolygonElement Cursor;

        private PolygonElement CursorBorder;

        private int InternalFPS = 0;
        private SortedList<int, SceneElement> I_Elements { get; set; } = new SortedList<int, SceneElement>();
        private SortedList<string, int> GUID_INDEX { get; set; } = new SortedList<string, int>();
        private List<int> RemoveQueue { get; set; } = new List<int>();
        private List<SceneElement> AddQueue { get; set; } = new List<SceneElement>();
        protected internal GraphicsDevice graphicsDevice {
            get {
                return (GraphicsDevice)sender().Services.GetService(typeof(GraphicsDevice));
            }
        }
        protected internal ProjectZ.Shared.Content.ContentContainer contentCollection {
            get {
                return (ProjectZ.Shared.Content.ContentContainer)sender().Services.GetService(typeof(ProjectZ.Shared.Content.ContentContainer));
            }
        }
        private SpriteBatch spriteBatch;

        // Cached RasterizerState to avoid creating new instances every frame
        private RasterizerState _scissorRasterizerState;
        #endregion

        #region Events

        #region Projection Hosts

        private List<SceneProjectionHost> ProjectionHosts = new List<SceneProjectionHost>();

        public void AddProjectionHost(SceneProjectionHost Target) {
            ProjectionHosts.Add(Target);
        }

        public void RemoveProjectionHost(SceneProjectionHost Target) {
            ProjectionHosts.Remove(Target);
        }

        public bool ContainsProjectionHost(SceneProjectionHost Target) {
            return ProjectionHosts.Contains(Target);
        }

        #endregion

        public event InitializedEventHandler Initialized;

        public delegate void InitializedEventHandler(GameTime gameTime);

        public event PreDrawEventHandler PreDraw;

        public delegate void PreDrawEventHandler(GameTime gameTime);
        public event PostDrawEventHandler PostDraw;

        public delegate void PostDrawEventHandler(GameTime gameTime);

        public event OnKeyPressEventHandler OnKeyPress;

        public delegate void OnKeyPressEventHandler(Keys Key, KeyboardState KeyboardState);
        public event OnKeyDownEventHandler OnKeyDown;

        public delegate void OnKeyDownEventHandler(Keys Key, KeyboardState KeyboardState);
        public event OnKeyUpEventHandler OnKeyUp;

        public delegate void OnKeyUpEventHandler(Keys Key, KeyboardState KeyboardState);

        public event OnMouseMoveEventHandler OnMouseMove;

        public delegate void OnMouseMoveEventHandler(Point currentPoint, Point lastPoint);
        public event OnMouseDragEventHandler OnMouseDrag;

        public delegate void OnMouseDragEventHandler(Point currentPoint, Point lastPoint);
        public event OnMouseDragDropEventHandler OnMouseDragDrop;

        public delegate void OnMouseDragDropEventHandler(Point currentPoint, Point lastPoint);


        public event OnMouseRightClickEventHandler OnMouseRightClick;

        public delegate void OnMouseRightClickEventHandler(Point p);
        public event OnMouseLeftClickEventHandler OnMouseLeftClick;

        public delegate void OnMouseLeftClickEventHandler(Point p);


        public event OnMouseLeftDownEventHandler OnMouseLeftDown;

        public delegate void OnMouseLeftDownEventHandler(Point p);
        public event OnMouseLeftUpEventHandler OnMouseLeftUp;

        public delegate void OnMouseLeftUpEventHandler(Point p);

        private Point MouseDownPoint;
        private bool MouseIsDown = false;  // Proper flag for tracking mouse state
        private SceneElement LastElement;
        private SceneElement DragDropElement;  // Saved for DragDrop event after MouseUp
        private SceneElement ClickTargetElement;  // Element that should receive click event
        private SceneElement HoveredElement;  // Currently hovered element for reliable enter/leave

        protected internal void MouseLeftDown(Point p) {
            OnMouseLeftDown?.Invoke(p);
            _MouseLeftDown(PointToElement(p), p);
        }

        private void _MouseLeftDown(SceneElement Element, Point p) {
            if (Element != null && Element.isEnabled) {
                LastElement = Element;
                ClickTargetElement = Element;  // Save for click event
                DragDropElement = Element;  // Save for potential drag-drop
                MouseDownPoint = p;
                MouseIsDown = true;
                Element.OnMouseLeftDown(p.Subtract(Element.Position));
                if (LastSelected is null) {
                    if (Element.CanSelect) {
                        Element.isSelected = true;
                        LastSelected = Element;
                    }
                }
            } else {
                ClickTargetElement = null;
                DragDropElement = null;
                if (LastSelected != null && LastSelected.isEnabled) {
                    if (LastSelected.CanSelect) {
                        LastSelected.isSelected = false;
                    }
                    LastSelected = null;
                }
            }
            if (LastSelected != null && LastSelected.isEnabled) {
                if (LastSelected.CanSelect) {
                    LastSelected.isSelected = false;
                    if (Element != null && Element.isEnabled) {
                        if (Element.CanSelect && !Element.isSelected) {
                            Element.isSelected = true;
                            LastSelected = Element;
                        }
                    }
                }
            }
            ProjectionHosts.ForEach(s => s.TargetScene.MouseLeftDown(s.Interp(p)));
        }

        protected internal void MouseLeftUp(Point p) {
            OnMouseLeftUp?.Invoke(p);

            // Store reference before clearing
            var elementToRelease = LastElement;

            // Always notify the element that received mouse down
            if (elementToRelease != null && elementToRelease.isEnabled) {
                elementToRelease.OnMouseLeftUp(p.Subtract(elementToRelease.Position));
                elementToRelease.OnUserInvalidated();
            }

            // Reset state but keep ClickTargetElement and DragDropElement for their respective events
            MouseIsDown = false;
            MouseDownPoint = Point.Zero;
            LastElement = null;
            LastMouseDrag = null;

            ProjectionHosts.ForEach(s => s.TargetScene.MouseLeftUp(s.Interp(p)));
        }

        protected internal void MouseLeftClick(Point p) {
            OnMouseLeftClick?.Invoke(p);

            // Use the element that received MouseDown, not a new hit test
            // This ensures clicks are reliable even if mouse moved slightly
            var Element = ClickTargetElement;
            if (Element != null && Element.isEnabled) {
                Element.OnMouseLeftClick(p.Subtract(Element.Position));
            }

            // Clear the click target after processing
            ClickTargetElement = null;

            ProjectionHosts.ForEach(s => s.TargetScene.MouseLeftClick(s.Interp(p)));
        }

        protected internal void MouseRightClick(Point p) {
            OnMouseRightClick?.Invoke(p);
            var Element = PointToElement(p);
            if (Element != null && Element.isEnabled) {
                Element.OnMouseRightClick(p.Subtract(Element.Position));
                Element.OnUserInvalidated();
            }

            ProjectionHosts.ForEach(s => s.TargetScene.MouseRightClick(s.Interp(p)));
        }

        protected internal void MouseDragDrop(Point currentPoint, Point lastPoint) {
            OnMouseDragDrop?.Invoke(currentPoint, lastPoint);

            // Use DragDropElement (set during MouseDown) or LastMouseDrag (set during drag) as fallback
            var draggedElement = DragDropElement ?? LastMouseDrag;
            if (draggedElement != null) {
                var dropTarget = PointToElement(currentPoint);
                draggedElement.OnMouseDragDrop(currentPoint, ref dropTarget);
                draggedElement.OnUserInvalidated();
            }

            // Reset drag state
            DragDropElement = null;
            LastMouseDrag = null;

            ProjectionHosts.ForEach(s => s.TargetScene.MouseDragDrop(s.Interp(currentPoint), s.Interp(lastPoint)));
        }

        protected internal void MouseDrag(Point currentPoint, Point lastPoint) {
            OnMouseDrag?.Invoke(currentPoint, lastPoint);

            // Use the element that was originally clicked (LastElement) or LastMouseDrag
            // Don't do a new hit test - the element being dragged should be the one that received MouseDown
            var elementToDrag = LastMouseDrag ?? LastElement;

            if (elementToDrag != null && elementToDrag.isEnabled && elementToDrag.isMouseDown) {
                var r_currentPoint = currentPoint.Subtract(elementToDrag.Position);
                var r_lastPoint = lastPoint.Subtract(elementToDrag.Position);

                // Check for drag over other elements
                var hoverElement = PointToElement(currentPoint);
                if (!ReferenceEquals(hoverElement, elementToDrag)) {
                    elementToDrag.OnMouseDragOver(r_currentPoint, ref hoverElement);
                }

                elementToDrag.OnMouseDrag(r_currentPoint, r_lastPoint);
                LastMouseDrag = elementToDrag;
            }

            ProjectionHosts.ForEach(s => s.TargetScene.MouseDrag(s.Interp(currentPoint), s.Interp(lastPoint)));
        }

        protected internal void MouseMove(Point currentPoint, Point lastPoint) {
            OnMouseMove?.Invoke(currentPoint, lastPoint);
            if (Cursor != null && isCursorVisible) {
                Cursor.Position = new Vector2(currentPoint.X + 1, currentPoint.Y + 1);
                CursorBorder.Position = new Vector2(currentPoint.X, currentPoint.Y);
            }

            var currentElement = PointToElement(currentPoint);

            // Handle mouse leave - use tracked HoveredElement for reliable state management
            if (HoveredElement != null && !ReferenceEquals(HoveredElement, currentElement)) {
                if (HoveredElement.isMouseOver) {
                    HoveredElement.isMouseOver = false;
                }
            }

            // Handle mouse enter - set isMouseOver on the element we're over
            if (currentElement != null && currentElement.isEnabled) {
                if (!currentElement.isMouseOver)
                    currentElement.isMouseOver = true;
                currentElement.OnMouseMove(currentPoint.Subtract(currentElement.Position), lastPoint.Subtract(currentElement.Position));
            }

            // Track the currently hovered element
            HoveredElement = currentElement;

            ProjectionHosts.ForEach(s => s.TargetScene.MouseMove(s.Interp(currentPoint), s.Interp(lastPoint)));
        }

        protected internal void SetMouseState(MouseState State) {
            _MouseState = State;
            ProjectionHosts.ForEach(s => s.TargetScene.SetMouseState(s.Interp(State)));
        }

        protected internal void KeyPress(Keys Key, KeyboardState KeyboardState) {
            OnKeyPress?.Invoke(Key, KeyboardState);
            if (LastSelected != null && LastSelected.isEnabled) {
                if (LastSelected.CanSelect && LastSelected.isSelected) {
                    LastSelected.KeyPress(Key, KeyboardState);
                }
            }

            ProjectionHosts.ForEach(s => s.TargetScene.KeyPress(Key, KeyboardState));
        }

        protected internal void KeyDown(Keys Key, KeyboardState KeyboardState) {
            OnKeyDown?.Invoke(Key, KeyboardState);
            if (LastSelected != null && LastSelected.isEnabled) {
                if (LastSelected.CanSelect && LastSelected.isSelected) {
                    LastSelected.KeyDown(Key, KeyboardState);
                }
            }
            ProjectionHosts.ForEach(s => s.TargetScene.KeyDown(Key, KeyboardState));
        }

        protected internal void KeyUp(Keys Key, KeyboardState KeyboardState) {
            OnKeyUp?.Invoke(Key, KeyboardState);
            if (LastSelected != null && LastSelected.isEnabled) {
                if (LastSelected.CanSelect && LastSelected.isSelected) {
                    LastSelected.KeyUp(Key, KeyboardState);
                    LastSelected.OnUserInvalidated();
                }
            }
            ProjectionHosts.ForEach(s => s.TargetScene.KeyUp(Key, KeyboardState));
        }

        #endregion

        #region Drawing Methods

        protected internal Texture2D WhitePlain;

        public Vector2 MeasureText(string Font, string Text) {
            return contentCollection.Fonts[Font].MeasureString(Text);
        }

        #endregion

        /// <summary>
        /// Finds the topmost element at the given point, checking children hierarchically.
        /// </summary>
        public SceneElement PointToElement(Point p) {
            // Iterate by actual keys in descending order (highest z-index first)
            var keys = I_Elements.Keys.ToList();
            for (int i = keys.Count - 1; i >= 0; i -= 1) {
                int zIndex = keys[i];
                var Element = I_Elements[zIndex];
                var hit = HitTestElement(Element, p);
                if (hit != null) {
                    return hit;
                }
            }
            return null;
        }

        /// <summary>
        /// Recursively hit tests an element and its children.
        /// Children are tested first (in reverse z-order) since they render on top.
        /// Returns the element that should receive the click, or Nothing if no hit.
        /// </summary>
        private SceneElement HitTestElement(SceneElement element, Point p) {
            if (!element.isVisible)
                return null;

            // First check if the point is even within this element's bounds
            bool elementContainsPoint = element.ContainsPoint(p);

            // Check children (they're on top of the parent)
            // Sort by z-index descending, then by add order descending
            if (element.Children.Count > 0) {
                var sortedChildren = element.Children.OrderByDescending(x => x.zIndex).ThenByDescending(x => element.Children.IndexOf(x)).ToList();
                foreach (var child in sortedChildren) {
                    var childHit = HitTestElement(child, p);
                    if (childHit != null) {
                        // If the child hit has isMouseBypassEnabled, return the parent instead
                        if (childHit.isMouseBypassEnabled) {
                            // Return this parent element if it doesn't also have bypass
                            if (!element.isMouseBypassEnabled) {
                                return element;
                            }
                            // Parent also has bypass, skip and let the parent's parent handle it
                            continue;
                        }
                        return childHit;
                    }

                    // Child returned Nothing, but check if the child itself contains the point and has bypass enabled
                    // This handles the case where the child's own ContainsPoint check passed but it returned Nothing due to bypass
                    if (child.isMouseBypassEnabled && child.ContainsPoint(p)) {
                        if (!element.isMouseBypassEnabled) {
                            return element;
                        }
                    }
                }
            }

            // Then check this element
            if (!element.isMouseBypassEnabled && elementContainsPoint) {
                return element;
            }

            return null;
        }

        /// <summary>
        /// Gets all elements at the given point, checking children hierarchically.
        /// </summary>
        public SceneElement[] PointToElements(Point p) {
            var Elements = new List<SceneElement>();
            // Iterate by actual keys in descending order (highest z-index first)
            var keys = I_Elements.Keys.ToList();
            for (int i = keys.Count - 1; i >= 0; i -= 1) {
                int zIndex = keys[i];
                var Element = I_Elements[zIndex];
                HitTestElementAll(Element, p, Elements);
            }
            return Elements.ToArray();
        }

        /// <summary>
        /// Recursively collects all elements at the given point.
        /// </summary>
        private void HitTestElementAll(SceneElement element, Point p, List<SceneElement> results) {
            if (!element.isVisible)
                return;

            bool elementContainsPoint = element.ContainsPoint(p);

            // Check children first (they're on top)
            if (element.Children.Count > 0) {
                var sortedChildren = element.Children.OrderByDescending(x => x.zIndex).ThenByDescending(x => element.Children.IndexOf(x)).ToList();
                foreach (var child in sortedChildren)
                    HitTestElementAll(child, p, results);
            }

            // Then check this element
            if (!element.isMouseBypassEnabled && elementContainsPoint) {
                results.Add(element);
            }
        }

        /// <summary>
        /// Adds a root element to the scene. Children are NOT added to the global list -
        /// they are rendered hierarchically within their parent's draw call.
        /// </summary>
        public void AddElement(SceneElement Element) {
            // Only add the element itself, not its children
            // Children are rendered as part of their parent's draw cycle
            AddQueue.Add(Element);
        }

        /// <summary>
        /// Removes a root element from the scene.
        /// </summary>
        public void RemoveElement(SceneElement Element) {
            // Use GUID_INDEX to find the actual z-index in case Element.zIndex is stale
            if (GUID_INDEX.ContainsKey(Element.GUID)) {
                int actualZIndex = GUID_INDEX[Element.GUID];
                RemoveQueue.Add(actualZIndex);
            }
            // Also remove from AddQueue if pending
            if (AddQueue.Contains(Element)) {
                AddQueue.Remove(Element);
            }
        }

        public bool ContainsElement(SceneElement Element) {
            return ContainsElement(Element.GUID);
        }

        public bool ContainsElement(string GUID) {
            if (GUID_INDEX.ContainsKey(GUID)) {
                int zIndex = GUID_INDEX[GUID];
                return I_Elements.ContainsKey(zIndex);
            }
            return false;
        }

        public SceneElement GetElement(string GUID) {
            return I_Elements[GUID_INDEX[GUID]];
        }

        public SceneElement GetElement(int zIndex) {
            return I_Elements[zIndex];
        }

        private void DrawElements(IEnumerable<SceneElement> Elements) {
            DrawElements(Elements, false);
        }

        /// <summary>
        /// Draws elements with clipping support.
        /// </summary>
        private void DrawElements(IEnumerable<SceneElement> Elements, bool ForceClip) {
            // Cache graphics device reference outside the loop
            var gd = spriteBatch.GraphicsDevice;
            var viewportRect = new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height);

            foreach (SceneElement E in Elements) {
                if (!E.isVisible)
                    continue;
                DrawElementRecursive(E, gd, viewportRect, ForceClip);
            }
        }

        /// <summary>
        /// Draws an element with optional clipping support.
        /// Children are already in I_Elements via _Children_ChildAdded and drawn in the main loop.
        /// </summary>
        private void DrawElementRecursive(SceneElement E, GraphicsDevice gd, Rectangle viewportRect, bool ForceClip) {
            if (!E.isVisible)
                return;

            long Start = Stopwatch.GetTimestamp();
            var previousScissor = gd.ScissorRectangle;
            bool needsClipRestore = false;

            if (E.Clip | ForceClip) {
                // Setup clipping
                var clipRect = E.Parent != null ? E.Parent.Rectangle : E.Rectangle;
                clipRect = Rectangle.Intersect(clipRect, viewportRect);
                clipRect = Rectangle.Intersect(clipRect, previousScissor);

                // Skip drawing if the scissor rectangle is empty
                if (clipRect.Width <= 0 || clipRect.Height <= 0) {
                    return;
                }

                // Enable scissor test if not already enabled
                bool scissorWasEnabled = false;
                try {
                    var rs = gd.RasterizerState;
                    scissorWasEnabled = rs != null && rs.ScissorTestEnable;
                } catch {
                }

                if (!scissorWasEnabled) {
                    if (_scissorRasterizerState is null) {
                        _scissorRasterizerState = new RasterizerState() {
                            CullMode = CullMode.None,
                            ScissorTestEnable = true
                        };
                    }
                    gd.RasterizerState = _scissorRasterizerState;
                }

                gd.ScissorRectangle = clipRect;
                needsClipRestore = true;
            }

            // Draw this element
            E.OnPreDraw(gameTime);
            E.ValidationCheck();
            E.doDraw(gameTime);

            // Notify draw finished
            var elapsedTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - Start);
            E.OnDrawFinished(elapsedTime);

            // Restore previous scissor rectangle if we changed it
            if (needsClipRestore) {
                gd.ScissorRectangle = previousScissor;
            }
        }

        public virtual void ApplyEffects() {
            if (Effects is null)
                return;
            foreach (Effect E in Effects) {
                foreach (EffectTechnique T in E.Techniques) {
                    foreach (EffectPass P in T.Passes)
                        P.Apply();
                }
            }
        }

        /// <summary>
        /// Updates the render target to match the current back buffer size.
        /// Call this when the window is resized.
        /// </summary>
        public void UpdateRenderTargetSize() {
            if (!isInitialized)
                return;

            int newWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
            int newHeight = graphicsDevice.PresentationParameters.BackBufferHeight;

            // Only recreate if size changed
            if (renderTarget != null && renderTarget.Width == newWidth && renderTarget.Height == newHeight) {
                return;
            }

            // Dispose old render target
            renderTarget?.Dispose();

            // Create new render target with updated size
            renderTarget = new RenderTarget2D(graphicsDevice, newWidth, newHeight, false, graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.Depth24Stencil8);
        }

        public RenderTarget2D DrawToRenderTarget() {
            RenderTarget2D DrawToRenderTargetRet = default;
            if (UseRenderTarget) {
                // Ensure render target matches current back buffer size
                UpdateRenderTargetSize();

                graphicsDevice.SetRenderTarget(renderTarget);
                graphicsDevice.Clear(BackgroundColor);

                PreDraw?.Invoke(gameTime);

                ApplyEffects();

                DrawElements(I_Elements.Values);

                if (!hasBegun) {
                    SpriteSettings.Begin(spriteBatch);
                    hasBegun = true;
                }

                DrawToRenderTargetRet = renderTarget;

                if (hasBegun) {
                    hasBegun = false;
                    spriteBatch.End();
                }

                graphicsDevice.SetRenderTarget(null);

                return DrawToRenderTargetRet;
            }
            return null;
        }

        public virtual void Draw(GameTime gameTime) {
            if (!isInitialized)
                return;

            if (UseRenderTarget) {
                var Texture = DrawToRenderTarget();
                if (!hasBegun) {
                    hasBegun = true;
                    RenderTargetOptions.Begin(spriteBatch);
                }
                spriteBatch.Draw(Texture, new Rectangle(0, 0, renderTarget.Width, renderTarget.Height), Color.White);
                if (hasBegun) {
                    hasBegun = false;
                    spriteBatch.End();
                }
                this.Texture = Texture;
            } else {
                graphicsDevice.Clear(BackgroundColor);
                PreDraw?.Invoke(gameTime);
                if (!hasBegun) {
                    hasBegun = true;
                    spriteBatch.Begin();
                }
                ApplyEffects();
                DrawElements(I_Elements.Values);
            }
            if (isCursorVisible) {
                CursorBorder.Draw(gameTime);
                Cursor.Draw(gameTime);
            }
            if (hasBegun) {
                hasBegun = false;
                spriteBatch.End();
            }

            PostDraw?.Invoke(gameTime);
        }

        internal Vector2[] CursorDefault = new[] { new Vector2(1f, 1f), new Vector2(3f, 10f), new Vector2(5f, 5f), new Vector2(9f, 5f) };
        internal Vector2[] CursorDefaultBorder = new[] { new Vector2(0f, 0f), new Vector2(4f, 12f), new Vector2(6f, 8f), new Vector2(12f, 6f) };

        internal Vector2[] CursorResizeLeft = new[] { new Vector2(0f, 6f), new Vector2(5f, 11f), new Vector2(6f, 10f), new Vector2(3f, 4f), new Vector2(10f, 5f), new Vector2(3f, 6f), new Vector2(5f, 0f), new Vector2(0f, 5f) };
        internal Vector2[] CursorResizeLeftBorder = new[] { new Vector2(0f, 0f), new Vector2(4f, 12f), new Vector2(6f, 8f), new Vector2(12f, 6f) };

        internal Vector2[] CursorResizeRight = new[] { new Vector2(1f, 1f), new Vector2(3f, 10f), new Vector2(5f, 5f), new Vector2(9f, 5f) };
        internal Vector2[] CursorResizeRightBorder = new[] { new Vector2(0f, 0f), new Vector2(4f, 12f), new Vector2(6f, 8f), new Vector2(12f, 6f) };

        internal Vector2[] CursorResizeTop = new[] { new Vector2(1f, 1f), new Vector2(3f, 10f), new Vector2(5f, 5f), new Vector2(9f, 5f) };
        internal Vector2[] CursorResizeTopBorder = new[] { new Vector2(0f, 0f), new Vector2(4f, 12f), new Vector2(6f, 8f), new Vector2(12f, 6f) };

        internal Vector2[] CursorResizeBottom = new[] { new Vector2(1f, 1f), new Vector2(3f, 10f), new Vector2(5f, 5f), new Vector2(9f, 5f) };
        internal Vector2[] CursorResizeBottomBorder = new[] { new Vector2(0f, 0f), new Vector2(4f, 12f), new Vector2(6f, 8f), new Vector2(12f, 6f) };

        public void ChangeCursorType(CursorType Cursor) {
            this.Cursor.ClearVectorPoints();
            switch (Cursor) {
                case CursorType.Default: {
                        this.Cursor.AddVectorPoints(CursorDefault);
                        break;
                    }
                case CursorType.ResizeBottom: {
                        this.Cursor.AddVectorPoints(CursorResizeBottom);
                        break;
                    }
                case CursorType.ResizeLeft: {
                        this.Cursor.AddVectorPoints(CursorResizeLeft);
                        break;
                    }
                case CursorType.ResizeRight: {
                        this.Cursor.AddVectorPoints(CursorResizeRight);
                        break;
                    }
                case CursorType.ResizeTop: {
                        this.Cursor.AddVectorPoints(CursorResizeTop);
                        break;
                    }
            }
        }


        public void ChangeCursorType(Vector2[] Cursor) {
            this.Cursor.ClearVectorPoints();
            this.Cursor.AddVectorPoints(Cursor);
        }

        public SpriteBatch GetSpriteBatch() {
            return spriteBatch;
        }

        public virtual void Initialize(GameTime gameTime) {
            this.gameTime = gameTime;
            CheckForAddedChildren();
            ProjectionHosts.ForEach(s => s.TargetScene.Initialize(gameTime));
            WhitePlain = ProjectZ.Shared.Content.Textures.CreateSolidTexture(graphicsDevice, Color.White);

            renderTarget = new RenderTarget2D(graphicsDevice, graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight, false, graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.Depth24Stencil8);

            if (SpriteSettings is null) {
                SpriteSettings = new ProjectZ.Shared.XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);
            }

            if (RenderTargetOptions is null) {
                RenderTargetOptions = new ProjectZ.Shared.XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone);
            }

            Cursor = new PolygonElement(this, new[] { new Vector2(1f, 1f), new Vector2(3f, 10f), new Vector2(5f, 5f), new Vector2(9f, 5f) }) { Size = new Vector2(4f) };
            Cursor.FillColor = Color.White;
            Cursor.Size = new Vector2(24f);

            CursorBorder = new PolygonElement(this, new[] { new Vector2(0f, 0f), new Vector2(4f, 12f), new Vector2(6f, 8f), new Vector2(12f, 6f) }) { Size = new Vector2(4f) };
            CursorBorder.FillColor = Color.Black;
            CursorBorder.Size = new Vector2(24f);

        }

        private void CheckForAddedChildren() {
            if (AddQueue.Count > 0) {
                // Process all items in the queue
                while (AddQueue.Count > 0) {
                    var Element = AddQueue[0];
                    AddQueue.RemoveAt(0);

                    // Generate GUID if missing
                    if (string.IsNullOrEmpty(Element.GUID)) {
                        Element.GUID = Guid.NewGuid().ToString();
                    }

                    // Skip if element already exists
                    if (GUID_INDEX.ContainsKey(Element.GUID)) {
                        continue;
                    }

                    // Always assign the next highest Z-index to ensure new elements appear on top
                    int nextIndex = 0;
                    if (I_Elements.Count > 0) {
                        nextIndex = I_Elements.Keys.Max() + 1;
                    }
                    Element.zIndex = nextIndex;

                    I_Elements.Add(Element.zIndex, Element);
                    GUID_INDEX.Add(Element.GUID, Element.zIndex);
                }
            }
        }

        private void CheckForRemovedChildren() {
            if (RemoveQueue.Count > 0) {
                for (int i = RemoveQueue.Count - 1; i >= 0; i -= 1) {
                    int x = RemoveQueue[i];
                    if (!I_Elements.ContainsKey(x)) {
                        continue;
                    }
                    var e = I_Elements[x];
                    if (ReferenceEquals(e.GetType(), typeof(SceneProjectionHost))) {
                        SceneProjectionHost h = (SceneProjectionHost)e;
                        if (ContainsProjectionHost(h))
                            RemoveProjectionHost(h);
                    }
                    GUID_INDEX.Remove(e.GUID);
                    I_Elements.Remove(x);
                }
                RemoveQueue.Clear();
            }
        }

        public virtual void Tick(GameTime gameTime) {
            // Process removals FIRST, then additions
            // This allows remove-and-readd patterns (like BringToFront) to work correctly
            CheckForRemovedChildren();
            CheckForAddedChildren();

            if (!isInitialized) {
                Initialize(gameTime);
                Initialized?.Invoke(gameTime);
                isInitialized = true;
            }

            // Tick all elements (children are already in I_Elements via _Children_ChildAdded)
            foreach (var element in I_Elements.Values)
                element.Tick(gameTime);

            InternalFPS += 1;
            int MS = gameTime.ElapsedGameTime.Milliseconds;
            if (MS != 0) {
                FPS = (int)Math.Round(1000d / gameTime.ElapsedGameTime.Milliseconds);
            }
            if (LastTick + OneSecond < gameTime.TotalGameTime.Ticks) {
                LastTick = gameTime.TotalGameTime.Ticks;
                InternalFPS = 0;
            }
        }

        public Scene() {

        }

        public Scene(SceneManager SceneManager) {
            InitialConstructor(SceneManager);
        }

        public void InitialConstructor(SceneManager SceneManager) {
            _sender = SceneManager.Sender;
            spriteBatch = (SpriteBatch)sender().Services.GetService(typeof(SpriteBatch));

            // Initialize effects list
            Effects = new List<Effect>();

            // Try to load FXAA shader effect from MonoGame content pipeline
            try {
                var FXAA = contentCollection.Content.Load<Effect>("FXAA");
                FXAA.Parameters["EdgeThreshold"].SetValue(0.125f);
                FXAA.Parameters["SubPixelAliasingRemoval"].SetValue(1.0f);
                Effects.Add(FXAA);
            } catch (Exception ex) {
                // FXAA shader not found or not compatible - continue without it
                Debug.WriteLine($"FXAA shader load failed: {ex.Message}");
            }

        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // Dispose managed resources
                    foreach (var element in I_Elements.Values)
                        element.Dispose();
                    I_Elements.Clear();
                    GUID_INDEX.Clear();

                    Cursor?.Dispose();
                    CursorBorder?.Dispose();

                    if (Effects != null) {
                        foreach (var effect in Effects)
                            effect?.Dispose();
                        Effects.Clear();
                    }

                    renderTarget?.Dispose();
                    WhitePlain?.Dispose();
                    _scissorRasterizerState?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }

    public enum CursorType {
        Default,
        ResizeLeft,
        ResizeTop,
        ResizeRight,
        ResizeBottom
    }

}