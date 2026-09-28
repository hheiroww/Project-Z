using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using TriangleNet;

namespace ProjectZ.Shared.Drawing.UI.Primitives {

    [Serializable]
    public class CircleElement : Advanced.PolygonElement {

        #region Properties

        public Point CenterPoint {
            get {
                return _CenterPoint;
            }
            set {
                _CenterPoint = value;
                CalculatePoints();
            }
        }
        private Point _CenterPoint;

        public int PointCount {
            get {
                return _PointCount;
            }
            set {
                _PointCount = value;
                CalculatePoints();
            }
        }
        private int _PointCount = 100;

        #endregion

        #region Constructors

        public CircleElement(Scene Scene) : base(Scene) {
            Init();
            SizeChanged += CircleElement_OnSizeChange;
            PreDraw += CircleElement_PreDraw;
            Trangulated += CircleElement_Trangulated;
        }

        public CircleElement(Scene Scene, bool newSpriteBatch) : base(Scene, newSpriteBatch) {
            Init();
            SizeChanged += CircleElement_OnSizeChange;
            PreDraw += CircleElement_PreDraw;
            Trangulated += CircleElement_Trangulated;
        }

        public CircleElement(Scene Scene, SpriteBatch spriteBatch) : base(Scene, spriteBatch) {
            Init();
            SizeChanged += CircleElement_OnSizeChange;
            PreDraw += CircleElement_PreDraw;
            Trangulated += CircleElement_Trangulated;
        }

        private void Init() {
            CanChange = false;
            Size = new Vector2(400f);
            CenterPoint = new Point((int)Math.Round(Size.X), (int)Math.Round(Size.Y));
            CanChange = true;
            CalculatePoints();
        }

        #endregion

        private void CalculatePoints() {
            if (!CanChange)
                return;
            CanChange = false;
            var Points = new List<Vector2>();
            int reoccurences = 0;
            for (int i = 0, loopTo = PointCount - 1; i <= loopTo; i++) {
                double x = _CenterPoint.X + Size.X * Math.Cos(2d * Math.PI * i / PointCount) / 2d - _CenterPoint.X / 2d;
                double y = _CenterPoint.Y + Size.Y * Math.Sin(2d * Math.PI * i / PointCount) / 2d - _CenterPoint.Y / 2d;
                var p = new Vector2((float)x, (float)y);
                var lp = Points.Count > 0 ? Points[i - 1] : default;
                if (lp != default && lp.X == p.X & lp.Y == p.Y) {
                    reoccurences += 1;
                }
                Points.Add(p);
            }

            if (reoccurences < PointCount - 2) {
                AddVectorPoints(Points.ToArray());
            }
            CanChange = true;
        }

        private bool CanChange = true;
        private bool doCalculateOnNextDraw = false;
        private void CircleElement_OnSizeChange(Vector2 oldSize, Vector2 newSize) {
            if (CanChange) {
                _CenterPoint = new Point((int)Math.Round(newSize.X), (int)Math.Round(newSize.Y));
                doCalculateOnNextDraw = true;
            }
        }

        private void CircleElement_PreDraw(GameTime gameTime) {
            if (doCalculateOnNextDraw & CanChange) {
                doCalculateOnNextDraw = false;
                CalculatePoints();
            }
        }

        private void CircleElement_Trangulated(Mesh newMesh) {

        }
    }

}