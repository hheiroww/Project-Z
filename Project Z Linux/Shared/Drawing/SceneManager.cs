using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
/* TODO ERROR: Skipped EndIfDirectiveTrivia
#End If
*/
namespace ProjectZ.Shared.Drawing {

    public class SceneManager : IDisposable {

        #region Properties

        // On Linux/cross-platform, UseHardwareInput and MouseHook are not supported.
        public int LimitFPS {
            get {
                return _LimitFPS;
            }
            set {
                _LimitFPS = Math.Max(Math.Min(value, 1000), 1);
                SetLimit = true;
            }
        }
        private int _LimitFPS = 60;
        private bool SetLimit = false;
        private bool isDragging { get; set; } = false;
        private Dictionary<string, Scene> Scenes { get; set; } = new Dictionary<string, Scene>();
        public Scene ActiveScene { get; set; }
        public Game Sender { get; set; }

        #endregion

        #region Input

        #region Keypress

        private KeyboardState LastKeyboardState = Keyboard.GetState();
        private long LastKeyHoldTick = 0L;
        private long LastKeyProcessTick = 0L;
        private const long OneSecond = 10000000L;

        private bool DoDuplicate = false;
        private Keys DuplicateKey;

        private void DetectKeyPress(GameTime gameTime) {
            var KeyboardState = Keyboard.GetState();
            long currentHoldTick = gameTime.TotalGameTime.Ticks;

            for (int i = 0; i <= 254; i++) {
                Keys Key = (Keys)i;
                if (KeyboardState.IsKeyDown(Key) & LastKeyboardState.IsKeyUp(Key)) {
                    LastKeyHoldTick = gameTime.TotalGameTime.Ticks;
                    ActiveScene.KeyDown(Key, KeyboardState);
                    DoDuplicate = false;
                    break;
                } else if (KeyboardState.IsKeyUp(Key) & LastKeyboardState.IsKeyDown(Key)) {
                    LastKeyHoldTick = gameTime.TotalGameTime.Ticks;
                    ActiveScene.KeyUp(Key, KeyboardState);
                    ProcessKeyPress(Key, KeyboardState);
                    DoDuplicate = false;
                    break;
                } else if (DoDuplicate && KeyboardState.IsKeyDown(Key) && currentHoldTick > LastKeyProcessTick + 2000000L) {
                    ProcessKeyPress(DuplicateKey, LastKeyboardState);
                    break;
                } else if (currentHoldTick > LastKeyHoldTick + 5000000L && KeyboardState.IsKeyDown(Key) & LastKeyboardState.IsKeyDown(Key)) {
                    LastKeyHoldTick = gameTime.TotalGameTime.Ticks;
                    LastKeyProcessTick = LastKeyHoldTick;
                    DuplicateKey = Key;
                    DoDuplicate = true;
                    break;
                }
            }

            LastKeyboardState = KeyboardState;
        }

        private void ProcessKeyPress(Keys PressedKey, KeyboardState keyboardState) {
            if (DebugEnabled) {
                switch (PressedKey) {
                    case Keys.F1: {
                            DebugParams.ShowFPS = !DebugParams.ShowFPS;
                            break;
                        }
                    case Keys.F2: {
                            DebugParams.ShowDrawFPS = !DebugParams.ShowDrawFPS;
                            break;
                        }
                    case Keys.F3: {
                            break;
                        }

                    case Keys.F4: {
                            break;
                        }

                    case Keys.F5: {
                            break;
                        }

                    case Keys.OemTilde: {
                            ConsoleEnabled = !ConsoleEnabled;
                            break;
                        }
                }
            }
            ActiveScene.KeyPress(PressedKey, keyboardState);
            PressedKey = default;
        }

        public static string TryConvertKeyboardInput(Keys key, KeyboardState keyboard) {
            string ReturnString = string.Empty;
            bool shift = keyboard.IsKeyDown(Keys.LeftShift) | keyboard.IsKeyDown(Keys.RightShift);

            switch (key) {
                case Keys.Enter: {
                        ReturnString = ReturnString + Environment.NewLine;
                        break;
                    }
                // Alphabet keys
                case Keys.A: {
                        if (shift) {
                            ReturnString = ReturnString + "A";
                        } else {
                            ReturnString = ReturnString + "a";
                        }

                        break;
                    }
                case Keys.B: {
                        if (shift) {
                            ReturnString = ReturnString + "B";
                        } else {
                            ReturnString = ReturnString + "b";
                        }

                        break;
                    }
                case Keys.C: {
                        if (shift) {
                            ReturnString = ReturnString + "C";
                        } else {
                            ReturnString = ReturnString + "c";
                        }

                        break;
                    }
                case Keys.D: {
                        if (shift) {
                            ReturnString = ReturnString + "D";
                        } else {
                            ReturnString = ReturnString + "d";
                        }

                        break;
                    }

                case Keys.E: {
                        if (shift) {
                            ReturnString = ReturnString + "E";
                        } else {
                            ReturnString = ReturnString + "e";
                        }

                        break;
                    }
                case Keys.F: {
                        if (shift) {
                            ReturnString = ReturnString + "F";
                        } else {
                            ReturnString = ReturnString + "f";
                        }

                        break;
                    }
                case Keys.G: {
                        if (shift) {
                            ReturnString = ReturnString + "G";
                        } else {
                            ReturnString = ReturnString + "g";
                        }

                        break;
                    }

                case Keys.H: {
                        if (shift) {
                            ReturnString = ReturnString + "H";
                        } else {
                            ReturnString = ReturnString + "h";
                        }

                        break;
                    }
                case Keys.I: {
                        if (shift) {
                            ReturnString = ReturnString + "I";
                        } else {
                            ReturnString = ReturnString + "i";
                        }

                        break;
                    }
                case Keys.J: {
                        if (shift) {
                            ReturnString = ReturnString + "J";
                        } else {
                            ReturnString = ReturnString + "j";
                        }

                        break;
                    }
                case Keys.K: {
                        if (shift) {
                            ReturnString = ReturnString + "K";
                        } else {
                            ReturnString = ReturnString + "k";
                        }

                        break;
                    }
                case Keys.L: {
                        if (shift) {
                            ReturnString = ReturnString + "L";
                        } else {
                            ReturnString = ReturnString + "l";
                        }

                        break;
                    }
                case Keys.M: {
                        if (shift) {
                            ReturnString = ReturnString + "M";
                        } else {
                            ReturnString = ReturnString + "m";
                        }

                        break;
                    }
                case Keys.N: {
                        if (shift) {
                            ReturnString = ReturnString + "N";
                        } else {
                            ReturnString = ReturnString + "n";
                        }

                        break;
                    }
                case Keys.O: {
                        if (shift) {
                            ReturnString = ReturnString + "O";
                        } else {
                            ReturnString = ReturnString + "o";
                        }

                        break;
                    }
                case Keys.P: {
                        if (shift) {
                            ReturnString = ReturnString + "P";
                        } else {
                            ReturnString = ReturnString + "p";
                        }

                        break;
                    }
                case Keys.Q: {
                        if (shift) {
                            ReturnString = ReturnString + "Q";
                        } else {
                            ReturnString = ReturnString + "q";
                        }

                        break;
                    }
                case Keys.R: {
                        if (shift) {
                            ReturnString = ReturnString + "R";
                        } else {
                            ReturnString = ReturnString + "r";
                        }

                        break;
                    }
                case Keys.S: {
                        if (shift) {
                            ReturnString = ReturnString + "S";
                        } else {
                            ReturnString = ReturnString + "s";
                        }

                        break;
                    }
                case Keys.T: {
                        if (shift) {
                            ReturnString = ReturnString + "T";
                        } else {
                            ReturnString = ReturnString + "t";
                        }

                        break;
                    }
                case Keys.U: {
                        if (shift) {
                            ReturnString = ReturnString + "U";
                        } else {
                            ReturnString = ReturnString + "u";
                        }

                        break;
                    }
                case Keys.V: {
                        if (shift) {
                            ReturnString = ReturnString + "V";
                        } else {
                            ReturnString = ReturnString + "v";
                        }

                        break;
                    }
                case Keys.W: {
                        if (shift) {
                            ReturnString = ReturnString + "W";
                        } else {
                            ReturnString = ReturnString + "w";
                        }

                        break;
                    }
                case Keys.X: {
                        if (shift) {
                            ReturnString = ReturnString + "X";
                        } else {
                            ReturnString = ReturnString + "x";
                        }

                        break;
                    }
                case Keys.Y: {
                        if (shift) {
                            ReturnString = ReturnString + "Y";
                        } else {
                            ReturnString = ReturnString + "y";
                        }

                        break;
                    }
                case Keys.Z: {
                        if (shift) {
                            ReturnString = ReturnString + "Z";
                        } else {
                            ReturnString = ReturnString + "z";
                        }

                        break;
                    }
                // Decimal keys
                case Keys.D0: {
                        if (shift) {
                            ReturnString = ReturnString + ")";
                        } else {
                            ReturnString = ReturnString + "0";
                        }

                        break;
                    }
                case Keys.D1: {
                        if (shift) {
                            ReturnString = ReturnString + "!";
                        } else {
                            ReturnString = ReturnString + "1";
                        }

                        break;
                    }
                case Keys.D2: {
                        if (shift) {
                            ReturnString = ReturnString + "@";
                        } else {
                            ReturnString = ReturnString + "2";
                        }

                        break;
                    }

                case Keys.D3: {
                        if (shift) {
                            ReturnString = ReturnString + "#";
                        } else {
                            ReturnString = ReturnString + "3";
                        }

                        break;
                    }
                case Keys.D4: {
                        if (shift) {
                            ReturnString = ReturnString + "$";
                        } else {
                            ReturnString = ReturnString + "4";
                        }

                        break;
                    }
                case Keys.D5: {
                        if (shift) {
                            ReturnString = ReturnString + "%";
                        } else {
                            ReturnString = ReturnString + "5";
                        }

                        break;
                    }
                case Keys.D6: {
                        if (shift) {
                            ReturnString = ReturnString + "^";
                        } else {
                            ReturnString = ReturnString + "6";
                        }

                        break;
                    }
                case Keys.D7: {
                        if (shift) {
                            ReturnString = ReturnString + "&";
                        } else {
                            ReturnString = ReturnString + "7";
                        }

                        break;
                    }
                case Keys.D8: {
                        if (shift) {
                            ReturnString = ReturnString + "*";
                        } else {
                            ReturnString = ReturnString + "8";
                        }

                        break;
                    }
                case Keys.D9: {
                        if (shift) {
                            ReturnString = ReturnString + "(";
                        } else {
                            ReturnString = ReturnString + "9";
                        }

                        break;
                    }
                // Decimal numpad keys
                case Keys.NumPad0: {
                        ReturnString = ReturnString + "0";
                        break;
                    }
                case Keys.NumPad1: {
                        ReturnString = ReturnString + "1";
                        break;
                    }
                case Keys.NumPad2: {
                        ReturnString = ReturnString + "2";
                        break;
                    }
                case Keys.NumPad3: {
                        ReturnString = ReturnString + "3";
                        break;
                    }
                case Keys.NumPad4: {
                        ReturnString = ReturnString + "4";
                        break;
                    }
                case Keys.NumPad5: {
                        ReturnString = ReturnString + "5";
                        break;
                    }
                case Keys.NumPad6: {
                        ReturnString = ReturnString + "6";
                        break;
                    }
                case Keys.NumPad7: {
                        ReturnString = ReturnString + "7";
                        break;
                    }
                case Keys.NumPad8: {
                        ReturnString = ReturnString + "8";
                        break;
                    }
                case Keys.NumPad9: {
                        ReturnString = ReturnString + "9";
                        break;
                    }
                // Special keys
                case Keys.OemTilde: {
                        if (shift) {
                            ReturnString = ReturnString + "~";
                        } else {
                            ReturnString = ReturnString + "`";
                        }

                        break;
                    }
                case Keys.OemSemicolon: {
                        if (shift) {
                            ReturnString = ReturnString + ":";
                        } else {
                            ReturnString = ReturnString + ";";
                        }

                        break;
                    }
                case Keys.OemQuotes: {
                        if (shift) {
                            ReturnString = ReturnString + "\"";
                        } else {
                            ReturnString = ReturnString + "'";
                        }

                        break;
                    }
                case Keys.OemQuestion: {
                        if (shift) {
                            ReturnString = ReturnString + "?";
                        } else {
                            ReturnString = ReturnString + "/";
                        }

                        break;
                    }
                case Keys.OemPlus: {
                        if (shift) {
                            ReturnString = ReturnString + "+";
                        } else {
                            ReturnString = ReturnString + "=";
                        }

                        break;
                    }
                case Keys.OemPipe: {
                        if (shift) {
                            ReturnString = ReturnString + "|";
                        } else {
                            ReturnString = ReturnString + @"\";
                        }

                        break;
                    }
                case Keys.OemPeriod: {
                        if (shift) {
                            ReturnString = ReturnString + ">";
                        } else {
                            ReturnString = ReturnString + ".";
                        }

                        break;
                    }
                case Keys.OemOpenBrackets: {
                        if (shift) {
                            ReturnString = ReturnString + "{";
                        } else {
                            ReturnString = ReturnString + "[";
                        }

                        break;
                    }
                case Keys.OemCloseBrackets: {
                        if (shift) {
                            ReturnString = ReturnString + "}";
                        } else {
                            ReturnString = ReturnString + "]";
                        }

                        break;
                    }
                case Keys.OemMinus: {
                        if (shift) {
                            ReturnString = ReturnString + "_";
                        } else {
                            ReturnString = ReturnString + "-";
                        }

                        break;
                    }
                case Keys.OemComma: {
                        if (shift) {
                            ReturnString = ReturnString + "<";
                        } else {
                            ReturnString = ReturnString + ",";
                        }

                        break;
                    }
                case Keys.Space: {
                        ReturnString = ReturnString + " ";
                        break;
                    }
            }

            return ReturnString;
        }

        #endregion

        #region Mouse

        private MouseState LastState = default;
        private Point StartPoint = default;
        private bool MouseDown = false;
        private int DragThreshold = 2;

        // MouseHook != available on Linux/cross-platform.
        private Point LastPoint = default;

        // Windows-specific: Get actual titlebar and border sizes from System.Windows.Forms
        // On Linux/cross-platform, no window chrome offset needed
        private readonly int TitlebarHeight = 0;
        private readonly int BorderWidth = 0;
        /* TODO ERROR: Skipped ElseDirectiveTrivia
        #Else
        *//* TODO ERROR: Skipped DisabledTextTrivia
        #Disable Warning IDE0051, IDE0052 ' Suppress unused member warnings - these are platform stubs
                ' Non-Windows platforms: Default to 0 (no window chrome offset needed)
                Private ReadOnly TitlebarHeight As Integer = 0
                Private ReadOnly BorderWidth As Integer = 0
        #Enable Warning IDE0051, IDE0052
        *//* TODO ERROR: Skipped EndIfDirectiveTrivia
        #End If
        */
        // MouseHook event handlers are not available on Linux/cross-platform.
        private void DetectMouseEvents(GameTime gameTime) {
            var State = Mouse.GetState();
            var scaledPosition = ScaleMousePosition(State.Position);
            var scaledState = new MouseState(scaledPosition.X, scaledPosition.Y, State.ScrollWheelValue, State.LeftButton, State.MiddleButton, State.RightButton, State.XButton1, State.XButton2);
            ActiveScene.SetMouseState(scaledState);

            // Left Button
            if (LastState.LeftButton == ButtonState.Pressed & State.LeftButton == ButtonState.Released) {
                MouseDown = false;
                ActiveScene.MouseLeftUp(scaledPosition);
                var Difference = GetDifference(StartPoint, scaledPosition);
                if (Difference.X > DragThreshold | Difference.Y > DragThreshold) {
                    // Mouse was dragged
                    ActiveScene.MouseDragDrop(scaledPosition, StartPoint);
                } else {
                    // Mouse was clicked
                    ActiveScene.MouseLeftClick(scaledPosition);
                }
            } else if (State.LeftButton == ButtonState.Pressed & !MouseDown) {
                StartPoint = scaledPosition;
                MouseDown = true;
                ActiveScene.MouseLeftDown(scaledPosition);
            }

            // Right Button
            if (LastState.RightButton == ButtonState.Pressed) {
                // Mouse was clicked
                ActiveScene.MouseRightClick(scaledPosition);
            }

            if (scaledPosition != LastState.Position) {
                if (MouseDown) {
                    var Difference = GetDifference(StartPoint, scaledPosition);
                    if (isDragging || Difference.X > DragThreshold | Difference.Y > DragThreshold) {
                        // Mouse was dragged
                        isDragging = true;
                        ActiveScene.MouseDrag(scaledPosition, StartPoint);
                    }
                } else if (isDragging) {
                    isDragging = false;
                }
                ActiveScene.MouseMove(scaledPosition, LastState.Position);
            }

            LastState = scaledState;
        }

        private Point ScaleMousePosition(Point position) {
            // Get the actual rendering target size (back buffer)
            int backBufferWidth = Sender.GraphicsDevice.PresentationParameters.BackBufferWidth;
            int backBufferHeight = Sender.GraphicsDevice.PresentationParameters.BackBufferHeight;

            // Get the window's client area size
            var clientBounds = Sender.Window.ClientBounds;

            // Avoid division by zero
            if (clientBounds.Width <= 0 || clientBounds.Height <= 0)
                return position;

            // If back buffer matches client bounds, no scaling needed
            if (backBufferWidth == clientBounds.Width && backBufferHeight == clientBounds.Height) {
                return position;
            }

            // Mouse position is in window client coordinates
            // We need to scale it to match the back buffer coordinates
            float scaleX = backBufferWidth / (float)clientBounds.Width;
            float scaleY = backBufferHeight / (float)clientBounds.Height;

            return new Point((int)Math.Round(position.X * scaleX), (int)Math.Round(position.Y * scaleY));
        }

        private Point GetDifference(Point StartPoint, Point EndPoint) {
            int DifferenceX = 0;
            int DifferenceY = 0;
            if (StartPoint.X < EndPoint.X) {
                DifferenceX = EndPoint.X - StartPoint.X;
            } else {
                DifferenceX = StartPoint.X - EndPoint.X;
            }
            if (StartPoint.Y < EndPoint.Y) {
                DifferenceY = EndPoint.Y - StartPoint.Y;
            } else {
                DifferenceY = StartPoint.Y - EndPoint.Y;
            }
            return new Point(DifferenceX, DifferenceY);
        }

        #endregion

        #endregion

        #region Debugging

        #region Debug Properties

        public bool DebugEnabled { get; set; } = false;
        public bool ConsoleEnabled { get; set; } = false;
        public DebugParameters DebugParams { get; set; } = new DebugParameters();

        private int _DrawFPS = 0;
        private long LastDrawTick = 0L;
        #endregion

        private int I_DrawFPS = 0;
        private string LastDebugText = "";
        private Rectangle DebugHeaderRectangle = default;
        private Vector2 DebugPosition = new Vector2(4f, 4f);
        private Vector2 DebugTextPosition = default;
        private Vector2 DebugHeaderSize = default;
        private Rectangle DebugRectangle = default;
        private Color DebugHeaderColor = Color.LimeGreen;
        private string SmallFont = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(10);
        private string RegularFont = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(12);
        private string LargeFont = ProjectZ.Shared.Content.Fonts.SegoeUI.get_GetResourceName(18);

        private void DrawDebugText(GameTime gameTime) {
            if (DebugHeaderSize == default) {
                DebugHeaderSize = ActiveScene.MeasureText(LargeFont, Sender.Window.Title);
            }
            if (DebugTextPosition == default) {
                DebugTextPosition = new Vector2(DebugPosition.X, DebugPosition.Y + DebugHeaderSize.Y);
            }

            string DebugText = string.Empty;

            if (DebugParams.ShowFPS) {
                DebugText += string.Format("Tick FPS: {1:D3}{0}", new object[] { Environment.NewLine, ActiveScene.FPS });
            }

            if (DebugParams.ShowDrawFPS) {
                DebugText += string.Format("Draw FPS: {1}{0}", new object[] { Environment.NewLine, ActiveScene.DrawFPS });
            }

            if (DebugText.EndsWith(Environment.NewLine)) {
                DebugText = DebugText.Remove(DebugText.Length - 1).Trim();
            } else if (DebugText.StartsWith(Environment.NewLine)) {
                DebugText = DebugText.Remove(0, 1).Trim();
            }

            if (DebugRectangle == default | (LastDebugText ?? "") != (DebugText ?? "")) {
                var DebugTextSize = ActiveScene.MeasureText(RegularFont, DebugText);
                int LargestWidth = (int)Math.Round(DebugHeaderSize.X > DebugTextSize.X ? DebugHeaderSize.X : DebugTextSize.X);
                int RectHeight = (int)Math.Round(DebugTextSize.Y);
                DebugRectangle = new Rectangle((int)Math.Round(DebugTextPosition.X), (int)Math.Round(DebugTextPosition.Y), LargestWidth, RectHeight);
            }

            if (DebugHeaderRectangle == default) {
                DebugHeaderRectangle = new Rectangle((int)Math.Round(DebugPosition.X), (int)Math.Round(DebugPosition.Y), DebugRectangle.Width, (int)Math.Round(DebugHeaderSize.Y));
            }
            // ActiveScene.spriteBatch.Begin(Graphics.SpriteSortMode.Immediate, Graphics.BlendState.Opaque)
            // ActiveScene.spriteBatch.Draw(ActiveScene.WhitePlain, DebugHeaderRectangle, New Color(40, 40, 40))
            // ActiveScene.spriteBatch.DrawString(ActiveScene.contentCollection.Fonts(LargeFont), Sender.Window.Title, DebugPosition, DebugHeaderColor)

            // If DebugText <> String.Empty Then
            // ActiveScene.spriteBatch.Draw(ActiveScene.WhitePlain, DebugRectangle, New Color(20, 20, 20))
            // ActiveScene.spriteBatch.DrawString(ActiveScene.contentCollection.Fonts(RegularFont), DebugText, DebugTextPosition, Color.Snow)
            // End If
            // ActiveScene.spriteBatch.End()
        }

        #endregion

        public void Draw(GameTime gameTime) {
            if (ActiveScene != null) {
                ActiveScene.Draw(gameTime);
                if (DebugEnabled) {
                    I_DrawFPS += 1;
                    if (LastDrawTick + OneSecond < gameTime.TotalGameTime.Ticks) {
                        LastDrawTick = gameTime.TotalGameTime.Ticks;
                        _DrawFPS = I_DrawFPS;
                        ActiveScene.DrawFPS = _DrawFPS;
                        I_DrawFPS = 0;
                    }
                    DrawDebugText(gameTime);
                }
            }
        }

        public void Tick(GameTime gameTime) {
            if (SetLimit) {
                // FIX
                // gameTime.ElapsedGameTime =' TimeSpan.FromMilliseconds(1000 / LimitFPS)
            }

            if (ActiveScene != null) {
                DetectKeyPress(gameTime);
                DetectMouseEvents(gameTime);
                ActiveScene.Tick(gameTime);
            }
        }

        public void AddScene(string Name, Scene Scene) {
            if (string.IsNullOrEmpty(Name.Trim())) {
                throw new Exception("Invalid Scene Name.");
            } else if (Scenes.ContainsKey(Name)) {
                throw new Exception("The Scene Manager already contains Scene Name '" + Name + "'.");
            } else {
                Scenes.Add(Name, Scene);
            }
        }

        public void RemoveScene(string SceneName) {
            Scenes.Remove(SceneName);
        }

        public Scene GetScene(string SceneName) {
            return Scenes[SceneName];
        }

        public SceneManager(Game sender, int LimitFPS) {
            Sender = sender;
            this.LimitFPS = LimitFPS;
        }

        public SceneManager(Game sender) {
            Sender = sender;
            var argSceneManager = this;
            var DefaultScene = new DefaultScene(ref argSceneManager);
            AddScene("Default", DefaultScene);
            ActiveScene = DefaultScene;
        }

        public class DebugParameters {

            public bool ShowFPS { get; set; } = true;
            public bool ShowDrawFPS { get; set; } = true;

        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // Dispose all scenes
                    foreach (var scene in Scenes.Values)
                        scene?.Dispose();
                    Scenes.Clear();
                    ActiveScene = null;

                    // MouseHook uninstall not needed on Linux/cross-platform.
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

}