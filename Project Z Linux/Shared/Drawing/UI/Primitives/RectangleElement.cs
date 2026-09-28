using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SocketJack;
using System;

namespace ProjectZ.Shared.Drawing.UI.Primitives {

    [Serializable]
    public class RectangleElement : SceneElement {

        #region Properties

        protected internal Texture2D Texture;
        public virtual Color BackgroundColor { get; set; } = new Color(60, 60, 60);
        public virtual AlignmentType isResizable { get; set; } = AlignmentType.None;

        protected internal Color Color {
            get {
                return _Color;
            }
            set {
                _Color = value;
                try {
                    Texture = ProjectZ.Shared.Content.Textures.CreateSolidTexture(Scene.graphicsDevice, _Color);
                } catch (Exception ex) {
                }
            }
        }

        #endregion

        #region Animation Properties
        public Animations.Properties.BackgroundColorProperty BackgroundProperty;
        private Color _Color = Color.White;

        class _failedMemberConversionMarker1 {
        }

        #endregion

        #region Constructors

        /*#error Cannot convert FieldDeclarationSyntax - see comment for details
               Cannot convert FieldDeclarationSyntax, CONVERSION ERROR: Object reference not set to an instance of an object. in 'Friend BackgroundProperty A...' at character 1411
                          at ICSharpCode.CodeConverter.CSharp.DeclarationNodeVisitor.<CreateAdditionalLocalMembers>d__61.MoveNext()
                          at ICSharpCode.CodeConverter.CSharp.DeclarationNodeVisitor.<CreateMemberDeclarations>d__59.MoveNext()
                          at System.Collections.Generic.List`1.InsertRange(Int32 index, IEnumerable`1 collection)
                          at ICSharpCode.CodeConverter.CSharp.DeclarationNodeVisitor.<GetMemberDeclarationsAsync>d__58.MoveNext()
                       --- End of stack trace from previous location where exception was thrown ---
                          at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                          at ICSharpCode.CodeConverter.CSharp.DeclarationNodeVisitor.<VisitFieldDeclaration>d__57.MoveNext()
                       --- End of stack trace from previous location where exception was thrown ---
                          at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                          at ICSharpCode.CodeConverter.CSharp.CommentConvertingVisitorWrapper.<ConvertHandledAsync>d__12`1.MoveNext()

                       Input:

                       #End Region

                       #Region "Animation Properties"

                               

                        */
        public RectangleElement() : base() {
            BackgroundProperty = new ProjectZ.Shared.Animations.Properties.BackgroundColorProperty();
            MouseMove += RectangleElement_MouseMove;
            Loaded += RectangleElement_Loaded;
        }

        public RectangleElement(Scene Scene) : base(Scene) {
            BackgroundProperty = new ProjectZ.Shared.Animations.Properties.BackgroundColorProperty();
            try {
                Texture = ProjectZ.Shared.Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White);
            } catch (Exception ex) {

            }

            MouseMove += RectangleElement_MouseMove;
            Loaded += RectangleElement_Loaded;
        }

        public RectangleElement(Scene Scene, SpriteBatch spriteBatch) : base(Scene, ref spriteBatch) {
            BackgroundProperty = new ProjectZ.Shared.Animations.Properties.BackgroundColorProperty();
            Texture = ProjectZ.Shared.Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White);
            MouseMove += RectangleElement_MouseMove;
            Loaded += RectangleElement_Loaded;
        }

        public RectangleElement(Scene Scene, bool newSpritebatch) : base(Scene, newSpritebatch) {
            BackgroundProperty = new ProjectZ.Shared.Animations.Properties.BackgroundColorProperty();
            Texture = ProjectZ.Shared.Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White);
            MouseMove += RectangleElement_MouseMove;
            Loaded += RectangleElement_Loaded;
        }

        #endregion

        protected internal override void Draw(GameTime gameTime) {
            if (Texture is null)
                Texture = ProjectZ.Shared.Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White);
            spriteBatch.Draw(Texture, Rectangle, BackgroundColor);
        }

        private void RectangleElement_MouseMove(Point currentPoint, Point lastPoint) {
            alignment(currentPoint, lastPoint);
        }

        private void alignment(Point currentPoint, Point lastPoint) {
            if (isResizable == AlignmentType.Horizontal) {
                if (currentPoint.X >= Size.X - 4f) {
                    EnableResizeCursor(AlignmentType.Horizontal, 1);
                } else if (currentPoint.X <= 4) {
                    EnableResizeCursor(AlignmentType.Horizontal, -1);
                } else {
                    Scene.ChangeCursorType(CursorType.Default);
                }
            } else if (isResizable == AlignmentType.Vertical) {
                if (currentPoint.Y >= Size.Y - 4f) {
                    EnableResizeCursor(AlignmentType.Vertical, 1);
                } else if (currentPoint.Y <= 4) {
                    EnableResizeCursor(AlignmentType.Vertical, -1);
                }
            } else {
                Scene.ChangeCursorType(CursorType.Default);
            }
        }

        private void EnableResizeCursor(AlignmentType @type, int location) {
            if (location < 0 && type == AlignmentType.Horizontal) {
                Scene.ChangeCursorType(CursorType.ResizeLeft);

            } else if (location < 0 && type == AlignmentType.Vertical) {
                Scene.ChangeCursorType(CursorType.ResizeTop);
            }
            if (location > 0 && type == AlignmentType.Horizontal) {
                Scene.ChangeCursorType(CursorType.ResizeRight);
            } else if (location > 0 && type == AlignmentType.Vertical) {
                Scene.ChangeCursorType(CursorType.ResizeBottom);
            }
        }

        private void RectangleElement_Loaded() {

        }
    }

}