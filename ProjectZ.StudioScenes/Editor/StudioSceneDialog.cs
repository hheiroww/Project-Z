using System.Diagnostics;
using System.Drawing.Imaging;

namespace ProjectZ.StudioScenes;

/// <summary>Edits the same scene document in any Windows host, with a Project-Z canvas inside a native modal window.</summary>
public sealed partial class StudioSceneDialog : Form
{
    private readonly StudioWindowSurface _surface;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private readonly Stopwatch _clock = new();
    private ProjectZCanvas? _canvas;
    private StudioSceneRender? _renderer;
    private Bitmap? _frame;
    private int[] _scenePixels = [];
    private int _sceneWidth;
    private int _sceneHeight;
    private double _lastSceneFrame = -1;
    private string? _sceneFile;
    private string? _loadError;
    private string? _message;
    private int _loadRevision;
    private bool _ready;
    private bool _dirty = true;
    private bool _refreshScene = true;
    private bool _rendering;
    private bool _pickerOpen;
    private bool _closed;

    public StudioSceneDialog(StudioScene initial, string? modelPath = null)
    {
        ArgumentNullException.ThrowIfNull(initial);
        Scene = initial.Copy();
        ModelPath = modelPath;
        Text = "Project-Z 3D Scene Editor";
        ClientSize = new Size(1040, 672);
        MinimumSize = new Size(790, 530);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        _surface = new StudioWindowSurface(this) { Dock = DockStyle.Fill, TabStop = true };
        Controls.Add(_surface);
        _timer.Tick += (_, _) => DrawFrame();
        ResetPathEditor();
        BuildSettings();
    }

    public StudioScene Scene { get; private set; }
    public string? ModelPath { get; private set; }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _surface.Focus();
        _canvas = new ProjectZCanvas(DrawStudio);
        LoadRenderer();
        _timer.Start();
        DrawFrame();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_closed)
        {
            _closed = true;
            _loadRevision++;
            _timer.Dispose();
            _canvas?.Dispose();
            _renderer?.Dispose();
            _frame?.Dispose();
        }
        base.Dispose(disposing);
    }

    private bool CanConfirm => _ready && _loadError is null &&
        (!Scene.HasModel || !string.IsNullOrEmpty(ModelPath)) && ValidChain();

    private string Status => _loadError ?? _message ??
        (!_ready ? "Loading scene…"
            : Scene.HasModel && string.IsNullOrEmpty(ModelPath) ? "Choose a 3D model to preview this scene."
            : !ValidChain() ? "Choose a directly connected root, bend, and tip."
            : ModelPath is not null && Scene.HasModel ? Path.GetFileName(ModelPath) : "Scene files open in the visualizer and video editor.");

    private void Confirm()
    {
        if (!CanConfirm) return;
        try
        {
            Scene.Validate();
            DialogResult = DialogResult.OK;
        }
        catch (InvalidDataException error)
        {
            _message = error.Message;
            InvalidateCanvas();
        }
    }

    private void ChooseModel() => RunFileAction(() =>
    {
        using var picker = new OpenFileDialog
        {
            Filter = "3D models|*.glb;*.gltf;*.fbx;*.dae;*.obj|All files|*.*",
            CheckFileExists = true, Title = "Choose a 3D model"
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        ModelPath = picker.FileName;
        Scene.Model.Asset = Path.GetFileName(ModelPath);
        Scene.Model.Animation = -1;
        Scene.Model.Ik = null;
        _draftChain = null;
        if (Scene.Kind == StudioSceneKind.Terrain)
            Scene.Kind = StudioSceneKind.TerrainAndModel;
        SelectPage(3);
        LoadRenderer(autoSelectAnimation: true);
    });

    private void OpenScene() => RunFileAction(() =>
    {
        using var picker = new OpenFileDialog { Filter = "Project-Z 3D scene|*.pzscene3d", CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        var scene = StudioSceneFile.Load(picker.FileName);
        var modelPath = StudioSceneFile.ResolveModelPath(picker.FileName, scene);
        Scene = scene;
        ResetPathEditor();
        ModelPath = modelPath;
        _sceneFile = picker.FileName;
        _draftChain = null;
        _terrainPreset = 0;
        SelectPage(0);
        LoadRenderer();
    });

    private void SaveScene() => RunFileAction(() =>
    {
        using var picker = new SaveFileDialog
        {
            Filter = "Project-Z 3D scene|*.pzscene3d", DefaultExt = "pzscene3d", AddExtension = true,
            FileName = _sceneFile is null ? "Studio scene" : Path.GetFileName(_sceneFile),
            InitialDirectory = Path.GetDirectoryName(ModelPath ?? _sceneFile) ?? ""
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        var portable = Scene.Copy();
        portable.Model.Asset = portable.HasModel && !string.IsNullOrEmpty(ModelPath)
            ? StudioSceneFile.MakeModelRelative(picker.FileName, ModelPath) : "";
        StudioSceneFile.Save(picker.FileName, portable);
        _sceneFile = picker.FileName;
        _message = $"Saved {Path.GetFileName(picker.FileName)}";
    });

    private void RunFileAction(Action action)
    {
        _pickerOpen = true;
        try { action(); }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or ArgumentException
            or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _message = error.GetBaseException().Message;
        }
        finally
        {
            _pickerOpen = false;
            if (!_closed) _surface.Focus();
            InvalidateCanvas();
        }
    }

    private async void LoadRenderer(bool autoSelectAnimation = false)
    {
        var current = ++_loadRevision;
        var snapshot = RenderableScene().Copy();
        var modelPath = ModelPath;
        if (snapshot.HasModel && string.IsNullOrEmpty(modelPath))
            snapshot.Kind = StudioSceneKind.Terrain;
        _ready = false;
        _renderer?.Dispose();
        _renderer = null;
        _loadError = _message = null;
        _scenePixels = [];
        BuildSettings();
        InvalidateCanvas();
        try
        {
            var renderer = await Task.Run(() => new StudioSceneRender(snapshot, modelPath));
            if (_closed || current != _loadRevision)
            {
                renderer.Dispose();
                return;
            }
            _renderer = renderer;
            if (autoSelectAnimation && renderer.Animations.Count > 0)
                Scene.Model.Animation = 0;
            _ready = true;
            BuildSettings();
            _clock.Restart();
            InvalidateCanvas(sceneChanged: true);
        }
        catch (Exception error)
        {
            if (!_closed && current == _loadRevision)
            {
                _loadError = error.GetBaseException().Message;
                InvalidateCanvas();
            }
        }
    }

    private bool ValidChain()
    {
        if (!Scene.HasModel || Scene.Model.Ik is not { } ik)
            return true;
        var joints = _renderer?.Joints;
        return joints is not null && (uint)ik.Root < joints.Count && (uint)ik.Bend < joints.Count &&
            (uint)ik.Tip < joints.Count && ik.Root < ik.Bend && ik.Bend < ik.Tip &&
            joints[ik.Bend].Parent == ik.Root && joints[ik.Tip].Parent == ik.Bend;
    }

    private StudioScene RenderableScene()
    {
        var ik = Scene.Model.Ik;
        if (ik is null || (ik.Root >= 0 && ik.Bend > ik.Root && ik.Tip > ik.Bend && ik.Tip < 16384 &&
            (!Scene.HasModel || _renderer is null || ValidChain())))
            return Scene;
        return Scene with { Model = Scene.Model with { Ik = null } };
    }

    private void InvalidateCanvas(bool sceneChanged = false)
    {
        _dirty = true;
        _refreshScene |= sceneChanged;
    }

    private void DrawFrame()
    {
        if (_rendering || _closed || _pickerOpen || _canvas is null || !Visible || WindowState == FormWindowState.Minimized)
            return;
        var width = _surface.ClientSize.Width;
        var height = _surface.ClientSize.Height;
        if (width < 2 || height < 2) return;
        var scale = Math.Min(1d, Math.Min(4096d / width, Math.Min(4096d / height, Math.Sqrt(8_388_608d / width / height))));
        width = Math.Max(2, (int)(width * scale));
        height = Math.Max(2, (int)(height * scale));

        if (_pathPlaying && !Scene.Motion.Loop && CurrentPreviewBeat() >= PreviewEndBeat())
            SetPlaying(false);
        var animationDue = _ready && PreviewIsMoving && _clock.Elapsed.TotalSeconds - _lastSceneFrame > .04;
        if (!_dirty && !animationDue) return;

        _rendering = true;
        _dirty = false;
        try
        {
            var pixels = _canvas.Render(width, height);
            if (_frame is null || _frame.Width != width || _frame.Height != height)
            {
                var next = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                _frame?.Dispose();
                _frame = next;
            }
            var bits = _frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                unsafe
                {
                    for (var row = 0; row < height; row++)
                        pixels.Slice(row * width, width).CopyTo(new Span<int>((bits.Scan0 + row * bits.Stride).ToPointer(), width));
                }
            }
            finally { _frame.UnlockBits(bits); }
            _surface.Invalidate();
        }
        catch (Exception error)
        {
            _timer.Stop();
            MessageBox.Show(this, error.GetBaseException().Message, "Project-Z renderer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
        }
        finally { _rendering = false; }
    }

    private void UpdatePreview(int width, int height)
    {
        if (_renderer is null || Scene.HasModel && string.IsNullOrEmpty(ModelPath) || width < 2 || height < 2)
            return;
        var now = _clock.Elapsed.TotalSeconds;
        var sizeChanged = width != _sceneWidth || height != _sceneHeight;
        var animate = PreviewIsMoving;
        if (!_refreshScene && !sizeChanged && (!animate || now - _lastSceneFrame <= .04))
            return;

        try
        {
            var renderFrame = PreviewFrameState(now);
            var pixels = _renderer.Render(width, height, renderFrame.Seconds, renderFrame.Scene, clock: renderFrame.Clock);
            if (_scenePixels.Length != pixels.Length)
                _scenePixels = new int[pixels.Length];
            pixels.CopyTo(_scenePixels);
            _sceneWidth = width;
            _sceneHeight = height;
            _lastSceneFrame = now;
            _refreshScene = false;
        }
        catch (Exception error)
        {
            _renderer.Dispose();
            _renderer = null;
            _loadError = error.GetBaseException().Message;
        }
    }

    private sealed class StudioWindowSurface : Control
    {
        private readonly StudioSceneDialog _dialog;

        public StudioWindowSurface(StudioSceneDialog dialog)
        {
            _dialog = dialog;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            AccessibleName = "Project-Z 3D scene drawing surface";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var frame = _dialog._frame;
            if (frame is not null)
                e.Graphics.DrawImage(frame, ClientRectangle);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_dialog._frame is null)
                e.Graphics.Clear(Color.FromArgb(15, 18, 25));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _dialog.InvalidateCanvas(sceneChanged: true);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Focus();
            var point = ToCanvas(e.Location);
            Capture = _dialog.PointerDown(point);
        }

        protected override void OnMouseMove(MouseEventArgs e) => _dialog.PointerMove(ToCanvas(e.Location));

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dialog.StopOrbit();
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            _dialog.StopOrbit();
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnMouseLeave(EventArgs e) => _dialog.PointerLeave();
        protected override void OnMouseWheel(MouseEventArgs e) => _dialog.PointerWheel(ToCanvas(e.Location), e.Delta);
        protected override bool ProcessCmdKey(ref Message msg, Keys keys) => _dialog.RouteKey(keys) || base.ProcessCmdKey(ref msg, keys);
        protected override void OnKeyPress(KeyPressEventArgs e) => e.Handled = _dialog.RouteText(e.KeyChar);

        private Point ToCanvas(Point point)
        {
            var frame = _dialog._frame;
            return frame is null ? point : new Point(
                (int)((long)point.X * frame.Width / Math.Max(1, ClientSize.Width)),
                (int)((long)point.Y * frame.Height / Math.Max(1, ClientSize.Height)));
        }
    }
}
