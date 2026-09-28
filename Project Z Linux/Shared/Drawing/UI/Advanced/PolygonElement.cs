using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing.UI.Primitives;
using SocketJack;
using System;
using System.Collections.Generic;

namespace ProjectZ.Shared.Drawing.UI.Advanced {

    public class PolygonElement : SceneElement {

        #region Properties

        public Vector2[] Vectors {
            get {
                return _vectorCache;
            }
            set {
                ClearVectorPoints();
                AddVectorPoints(value);
            }
        }

        public int[][] Segments {
            get {
                return _segmentCache;
            }
            set {
                ClearSegmentPoints();
                AddSegments(value);
            }
        }

        public TriangleNet.Mesh MeshData {
            get {
                return _MeshData;
            }
        }
        private TriangleNet.Mesh _MeshData;
        private TriangleNet.Mesh _FallbackMeshData;

        internal Texture2D Texture {
            get {
                return primitiveBatch.Texture;
            }
            set {
                primitiveBatch.Texture = value;
            }
        }

        public Color FillColor {
            get {
                return FillColorCache;
            }
            set {
                FillColorCache = value;
            }
        }
        private Color FillColorCache;

        public Vector2 Bounds { get; set; }

        public PolygonRenderProperties RenderProperties = new PolygonRenderProperties();

        #endregion

        #region Events


        #endregion

        #region Animation Properties

        public event TrangulatedEventHandler Trangulated;

        public delegate void TrangulatedEventHandler(TriangleNet.Mesh newMesh);

        class _failedMemberConversionMarker1 {
        }

        #endregion

        #region Internals

        /* error Cannot convert FieldDeclarationSyntax - see comment for details
                Cannot convert FieldDeclarationSyntax, CONVERSION ERROR: Object reference not set to an instance of an object. in 'Public FillColorProperty As...' at character 2478
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

                                Public FillColorProperty As New Global.ProjectZ.Shared.Animations.Properties.FillColorProperty(Me)

                         */


        public static Animations.Properties.FillColorProperty FillColorProperty;
        private Texture2D WhitePlain = null;
        private List<PrimitiveBatch> wireFrameHighlightPrimitiveBatchList = new List<PrimitiveBatch>();
        private PrimitiveBatch wireFramePrimitiveBatch;
        private PrimitiveBatch primitiveBatch;
        private bool Triangulated = false;

        #region Vector Methods
        private List<Vector2> _vectors = new List<Vector2>();
        private Vector2[] _vectorCache = Array.Empty<Vector2>();

        public Vector2 GetVectorPoint(int Index) {
            return Vectors[Index];
        }

        public void SetVectorPoint(int Index, Vector2 Vector) {
            lock (_vectors)
                _vectors[Index] = Vector;
        }

        public void AddVectorPoint(Vector2 Vector) {
            lock (_vectors)
                _vectors.Add(Vector);
        }

        public void AddVectorPoints(Vector2[] Vectors) {
            if (Vectors is null || Vectors.Length < 3)
                return;

            bool anyNonZero = false;
            var first = Vectors[0];
            bool anyDifferent = false;
            for (int i = 0, loopTo = Vectors.Length - 1; i <= loopTo; i++) {
                if (Vectors[i].X != 0f || Vectors[i].Y != 0f) {
                    anyNonZero = true;
                }
                if (!anyDifferent && (Vectors[i].X != first.X || Vectors[i].Y != first.Y)) {
                    anyDifferent = true;
                }
            }

            if (!anyNonZero || !anyDifferent) {
                Triangulated = false;
                return;
            }
            lock (_vectors) {
                ClearSegmentPoints();
                ClearVectorPoints();
                _vectors.AddRange(Vectors);
            }
            lock (_segments) {
                int segmentOffset = _segments.Count;
                boundaryMarkers.Add(_segments.Count);
                for (int i = 0, loopTo1 = Vectors.Length - 2; i <= loopTo1; i++)
                    _segments.Add(new[] { i + segmentOffset, i + 1 + segmentOffset });
                _segments.Add(new[] { Vectors.Length - 1 + segmentOffset, segmentOffset });
            }
        }

        public void RemoveVectorPoint(Vector2 Vector) {
            lock (_vectors)
                _vectors.Remove(Vector);
        }

        public void RemoveVectorPoints(Vector2[] Vectors) {
            lock (_vectors) {
                for (int i = Vectors.Length - 1; i >= 0; i -= 1)
                    _vectors.Remove(Vectors[i]);
            }
        }

        public bool ContainsVectorPoint(Vector2 Vector) {
            return _vectors.Contains(Vector);
        }

        public void ClearVectorPoints() {
            lock (_vectors)
                _vectors.Clear();
        }

        #endregion

        #region Segment Methods

        private List<int[]> _segments = new List<int[]>();
        private int[][] _segmentCache = Array.Empty<int[]>();
        private List<int> boundaryMarkers = new List<int>();

        public int[] GetSegment(int Index) {
            lock (_segments)
                return _segments[Index];
        }

        public void SetSegment(int Index, int[] Segment) {
            lock (_segments)
                _segments[Index] = Segment;
        }

        public void AddSegment(int[] Segment) {
            lock (_segments)
                _segments.Add(Segment);
        }

        public void AddSegments(int[][] Segments) {
            lock (_segments) {
                foreach (int[] Segment in Segments)
                    _segments.Add(Segment);
            }
        }

        public void RemoveSegment(int[] Segment) {
            lock (_segments)
                _segments.Remove(Segment);
        }

        public void RemoveSegments(int[][] Segments) {
            lock (_segments) {
                for (int i = Segments.Length - 1; i >= 0; i -= 1)
                    _segments.Remove(Segments[i]);
            }
        }

        public bool ContainsSegment(int[] Segment) {
            return _segments.Contains(Segment);
        }

        public void ClearSegmentPoints() {
            lock (_segments)
                _segments.Clear();
            boundaryMarkers.Clear();
            Triangulated = false;
        }

        #endregion

        private void Triangulate() {
            if (_vectors.Count < 3) {
                return;
            }

            // Convert _vectors to List(Of Vector2)
            var points = new List<Vector2>();
            foreach (Vector2 v in _vectors)
                points.Add(new Vector2(v.X, v.Y));

            // Convert _segments to List(Of Tuple(Of Integer, Integer))
            // Each segment is expected to be a pair of vertex indices: {fromIndex, toIndex}.
            var segments = new List<Tuple<int, int>>();
            foreach (int[] s in _segments) {
                if (s is null || s.Length < 2) {
                    continue;
                }

                int a = s[0];
                int b = s[1];
                if (a < 0 || b < 0 || a >= points.Count || b >= points.Count) {
                    continue;
                }

                segments.Add(Tuple.Create(a, b));
            }
            // Triangulate using TriangleNet
            var polygon = new TriangleNet.Geometry.Polygon();
            var vertices = new List<TriangleNet.Geometry.Vertex>(points.Count);

            for (int i = 0, loopTo = points.Count - 1; i <= loopTo; i++) {
                var vtx = new TriangleNet.Geometry.Vertex(points[i].X, points[i].Y);
                vertices.Add(vtx);
                polygon.Add(vtx);
            }

            foreach (var s in segments)
                polygon.Add(new TriangleNet.Geometry.Segment(vertices[s.Item1], vertices[s.Item2]));

            var mesher = new TriangleNet.Meshing.GenericMesher();
            TriangleNet.Mesh mesh = (TriangleNet.Mesh)mesher.Triangulate(polygon);
            _MeshData = mesh;
            _FallbackMeshData = mesh;

            // Calculate bounds
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (var pt in points) {
                if (pt.X < minX)
                    minX = pt.X;
                if (pt.Y < minY)
                    minY = pt.Y;
                if (pt.X > maxX)
                    maxX = pt.X;
                if (pt.Y > maxY)
                    maxY = pt.Y;
            }
            Bounds = new Vector2(maxX - minX, maxY - minY);

            Triangulated = true;

            Trangulated?.Invoke(_MeshData);
        }

        #endregion

        #region Constructors

        public PolygonElement() : base() {
            FillColorProperty = new ProjectZ.Shared.Animations.Properties.FillColorProperty();

        }

        public new void UpdateScene(Scene Scene) {
            InitializePolygon(Array.Empty<Vector2>());
            WhitePlain = Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White);
        }


        public PolygonElement(Scene Scene) : base(Scene, true) {
            FillColorProperty = new ProjectZ.Shared.Animations.Properties.FillColorProperty();
            InitializePolygon(Array.Empty<Vector2>());
        }

        public PolygonElement(Scene Scene, Vector2[] Vectors) : base(Scene, true) {
            FillColorProperty = new ProjectZ.Shared.Animations.Properties.FillColorProperty();
            InitializePolygon(Vectors);
        }

        public PolygonElement(Scene Scene, Vector2[] Vectors, bool newSpriteBatch) : base(Scene, newSpriteBatch) {
            FillColorProperty = new ProjectZ.Shared.Animations.Properties.FillColorProperty();
            InitializePolygon(Vectors);
        }

        public PolygonElement(Scene Scene, bool newSpriteBatch) : base(Scene, newSpriteBatch) {
            FillColorProperty = new ProjectZ.Shared.Animations.Properties.FillColorProperty();
            InitializePolygon(Array.Empty<Vector2>());
        }

        public PolygonElement(Scene Scene, Vector2[] Vectors, SpriteBatch spriteBatch) : base(Scene, ref spriteBatch) {
            FillColorProperty = new ProjectZ.Shared.Animations.Properties.FillColorProperty();
            InitializePolygon(Vectors);
        }

        public PolygonElement(Scene Scene, SpriteBatch spriteBatch) : base(Scene, ref spriteBatch) {
            FillColorProperty = new ProjectZ.Shared.Animations.Properties.FillColorProperty();
            InitializePolygon(Array.Empty<Vector2>());
        }

        private void InitializePolygon(Vector2[] Vectors) {
            FillColorProperty = new Animations.Properties.FillColorProperty();
            Size = new Vector2((float)spriteBatch.GraphicsDevice.Viewport.Bounds.Width, (float)spriteBatch.GraphicsDevice.Viewport.Bounds.Height);

            primitiveBatch = new PrimitiveBatch(spriteBatch.GraphicsDevice, WhitePlain, Color.White, 1000);
            wireFramePrimitiveBatch = new PrimitiveBatch(spriteBatch.GraphicsDevice, WhitePlain, RenderProperties.WireFrameColor, Vectors.Length * 2);

            RenderProperties.OnWireFrameColorChanged += c => wireFramePrimitiveBatch.Color = c;
            Triangulated = false;
            lock (_vectors) {
                _vectors.Clear();
                _vectorCache = _vectors.ToArray();
            }
            AddVectorPoints(Vectors);
            Triangulate();
        }

        #endregion

        public void ApplyGeometryChanges() {
            Triangulate();
        }

        protected internal override void Draw(GameTime gameTime) {

            if (!Triangulated)
                Triangulate();
            if (MeshData is null)
                return;
            // Fill Normally
            primitiveBatch.Color = new Color(FillColorCache, FillColorCache.A);
            primitiveBatch.Begin(PrimitiveType.TriangleList);
            foreach (var tri in MeshData.Triangles) {
                for (int i = 0; i <= 2; i++) {
                    var vt = tri.GetVertex(i);
                    var v = new Vector2((float)vt.X, (float)vt.Y);
                    primitiveBatch.AddVertex(Vector2.Subtract(v, new Vector2(-Position.X, -Position.Y)));
                }
            }
            primitiveBatch.End();

        }

        protected internal bool ContainsPoint(Vector2 p) {
            var RelativePoint = Vector2.Subtract(p, Position);
            if (MeshData != null) {
                foreach (var tri in MeshData.Triangles)
                    // Simple point-in-triangle test
                    // If pts.Length = 3 Then
                    // If PointInTriangle(RelativePoint, pts(0), pts(1), pts(2)) Then

                    // End If
                    // End If
                    return true;
            }
            return false;
        }

        protected internal bool ContainsPoint(Vector2 p, Vector2 a, Vector2 b, Vector2 c) {
            float px = p.X;
            float py = p.Y;
            float ax = a.X;
            float ay = a.Y;
            float bx = b.X;
            float @by = b.Y;
            float cx = c.X;
            float cy = c.Y;
            float v0x = cx - ax;
            float v0y = cy - ay;
            float v1x = bx - ax;
            float v1y = by - ay;
            float v2x = px - ax;
            float v2y = py - ay;
            float dot00 = v0x * v0x + v0y * v0y;
            float dot01 = v0x * v1x + v0y * v1y;
            float dot02 = v0x * v2x + v0y * v2y;
            float dot11 = v1x * v1x + v1y * v1y;
            float dot12 = v1x * v2x + v1y * v2y;
            float invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
            return u >= 0f & v >= 0f & u + v < 1f;
        }

    }

}