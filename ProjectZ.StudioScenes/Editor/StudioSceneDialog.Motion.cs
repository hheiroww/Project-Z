using System.Diagnostics;
using System.Globalization;

namespace ProjectZ.StudioScenes;

public sealed partial class StudioSceneDialog
{
    private readonly Stopwatch _pathClock = new();
    private StudioCameraKeyframe _draftPose = new();
    private int _selectedKey = -1;
    private bool _poseEditing;
    private bool _pathPlaying;
    private double _previewBeat;
    private double _playStartBeat;

    private StudioCamera InputCamera => _poseEditing && Scene.Motion.Mode == StudioCameraMode.Keyframes
        ? _draftPose.Camera : Scene.Camera;

    private bool PreviewIsMoving => Scene.Motion.Mode == StudioCameraMode.Static
        ? Scene.HasTerrain && Scene.Terrain.ShowWater || Scene.HasModel && Scene.Model.Animation >= 0
        : _pathPlaying;

    private EditorRow[] BuildPath()
    {
        var motion = Scene.Motion;
        var rows = new List<EditorRow>
        {
            new ChoiceRow("Camera motion", ["Static", "Flythrough", "Orbit", "Keyframes"],
                () => (int)motion.Mode, SetCameraMode),
        };
        if (motion.Mode == StudioCameraMode.Static)
            return rows.ToArray();

        rows.Add(new ActionRow("Preview path", () => _pathPlaying ? "Pause" : "Play",
            () => SetPlaying(!_pathPlaying)));
        rows.Add(new NumberRow("Preview beat", () => (float)CurrentPreviewBeat(), value => ScrubTo(value),
            0, (float)PreviewEndBeat(), .25f));
        rows.Add(new ChoiceRow("Beat grid", ["Free", "Bar", "Half note", "Quarter note", "Eighth note", "Sixteenth note"],
            () => (int)motion.Grid, value => motion.Grid = (StudioBeatGrid)value));
        if (motion.Mode == StudioCameraMode.Keyframes)
        {
            rows.Add(new ToggleRow("Edit pose", () => _poseEditing, SetPoseEditing));
            rows.Add(new ActionRow("Capture at beat", () => "Add or update", CaptureKeyframe));
            var names = motion.Keyframes.Select(key => "Beat " + key.Beat.ToString("0.###", CultureInfo.InvariantCulture));
            rows.Add(new ChoiceRow("Selected keyframe", ["New pose", .. names],
                () => _selectedKey + 1, value => SelectKeyframe(value - 1)));
            rows.Add(new ActionRow("Selected keyframe", () => "Delete", DeleteKeyframe,
                () => (uint)_selectedKey < motion.Keyframes.Count));
            if (_poseEditing)
            {
                rows.Add(new NumberRow("Position X", () => _draftPose.X, value => _draftPose.X = value, -1_000_000, 1_000_000, .1f));
                rows.Add(new NumberRow("Position Y", () => _draftPose.Y, value => _draftPose.Y = value, -1_000_000, 1_000_000, .1f));
                rows.Add(new NumberRow("Position Z", () => _draftPose.Z, value => _draftPose.Z = value, -1_000_000, 1_000_000, .1f));
                rows.Add(new NumberRow("Pose yaw", () => _draftPose.Camera.Yaw, value => _draftPose.Camera.Yaw = value, -3600, 3600, 1));
                rows.Add(new NumberRow("Pose pitch", () => _draftPose.Camera.Pitch, value => _draftPose.Camera.Pitch = value, -89, 89, 1));
                rows.Add(new NumberRow("Pose roll", () => _draftPose.Camera.Roll, value => _draftPose.Camera.Roll = value, -3600, 3600, 1));
                rows.Add(new NumberRow("Pose zoom", () => _draftPose.Camera.Zoom, value => _draftPose.Camera.Zoom = value, .2f, 3, .05f));
                rows.Add(new NumberRow("Pose field of view", () => _draftPose.Camera.FieldOfView,
                    value => _draftPose.Camera.FieldOfView = value, 18, 90, 1));
            }
        }
        else
        {
            rows.Add(new NumberRow("Distance per beat", () => motion.DistancePerBeat,
                value => motion.DistancePerBeat = value, 0, 128, .05f));
        }

        rows.Add(new ToggleRow("Sync to track", () => motion.SyncToTrack, value => motion.SyncToTrack = value));
        rows.Add(new ToggleRow("Loop path", () => motion.Loop, value => motion.Loop = value));
        rows.Add(new NumberRow("Preview tempo", () => (float)motion.Tempo, SetPreviewTempo, 20, 400, 1));
        rows.Add(new NumberRow("Duration in beats", () => (float)motion.DurationBeats, SetDuration, .25f, 16384, .25f));
        rows.Add(new NumberRow("Beats per bar", () => motion.BeatsPerBar, value => motion.BeatsPerBar = (int)value, 1, 32, 1));
        rows.Add(new NumberRow("Beat offset", () => (float)motion.PhaseOffsetBeats,
            value => motion.PhaseOffsetBeats = value, -16384, 16384, .25f));
        return rows.ToArray();
    }

    private void SetCameraMode(int selected)
    {
        Scene.Motion.Mode = (StudioCameraMode)selected;
        ResetPathEditor();
        BuildSettings();
        InvalidateCanvas(sceneChanged: true);
    }

    private void ResetPathEditor()
    {
        _pathClock.Reset();
        _previewBeat = 0;
        _playStartBeat = 0;
        _pathPlaying = _poseEditing = false;
        _selectedKey = -1;
        _draftPose = new StudioCameraKeyframe { Camera = Scene.Camera with { } };
    }

    private double PreviewEndBeat()
    {
        var motion = Scene.Motion;
        if (motion.Mode != StudioCameraMode.Keyframes || motion.Keyframes.Count == 0)
            return motion.DurationBeats;
        var lastBeat = motion.Keyframes[^1].Beat;
        return Math.Max(motion.DurationBeats, lastBeat + (motion.Loop ? .25 : 0));
    }

    private double CurrentPreviewBeat()
    {
        if (!_pathPlaying) return _previewBeat;
        var motion = Scene.Motion;
        var elapsed = _playStartBeat + _pathClock.Elapsed.TotalSeconds * motion.Tempo / 60;
        var end = PreviewEndBeat();
        return motion.Loop && end > 0 ? elapsed % end : Math.Min(elapsed, end);
    }

    private void SetPlaying(bool playing)
    {
        if (playing == _pathPlaying) return;
        _previewBeat = CurrentPreviewBeat();
        _pathPlaying = playing;
        if (playing)
        {
            if (_poseEditing)
            {
                _poseEditing = false;
                BuildSettings();
            }
            _playStartBeat = _previewBeat >= PreviewEndBeat() ? 0 : _previewBeat;
            _pathClock.Restart();
        }
        else _pathClock.Stop();
        InvalidateCanvas(sceneChanged: true);
    }

    private void ScrubTo(double beat)
    {
        SetPlaying(false);
        _previewBeat = Math.Clamp(beat, 0, PreviewEndBeat());
        InvalidateCanvas(sceneChanged: true);
    }

    private void SetPreviewTempo(float tempo)
    {
        var beat = CurrentPreviewBeat();
        Scene.Motion.Tempo = tempo;
        if (_pathPlaying)
        {
            _playStartBeat = beat;
            _pathClock.Restart();
        }
    }

    private void SetDuration(float beats)
    {
        var current = CurrentPreviewBeat();
        var keys = Scene.Motion.Keyframes;
        var required = keys.Count == 0 ? .25 : keys[^1].Beat;
        Scene.Motion.DurationBeats = Math.Max(beats, required);
        ScrubTo(current);
        BuildSettings();
    }

    private void SetPoseEditing(bool editing)
    {
        if (editing)
        {
            SetPlaying(false);
            var pose = SamplePreviewPose();
            _draftPose = new StudioCameraKeyframe
            {
                Beat = _previewBeat, Camera = pose.Camera with { }, X = pose.X, Y = pose.Y, Z = pose.Z
            };
        }
        _poseEditing = editing;
        BuildSettings();
        InvalidateCanvas(sceneChanged: true);
    }

    private void SelectKeyframe(int index)
    {
        var motion = Scene.Motion;
        _selectedKey = index;
        if ((uint)index < motion.Keyframes.Count)
        {
            var key = motion.Keyframes[index];
            ScrubTo(key.Beat);
            _draftPose = new StudioCameraKeyframe
            {
                Beat = key.Beat, Camera = key.Camera with { }, X = key.X, Y = key.Y, Z = key.Z
            };
        }
        else
        {
            var pose = SamplePreviewPose();
            _draftPose = new StudioCameraKeyframe
            {
                Beat = _previewBeat, Camera = pose.Camera with { }, X = pose.X, Y = pose.Y, Z = pose.Z
            };
        }
        _poseEditing = true;
        BuildSettings();
        InvalidateCanvas(sceneChanged: true);
    }

    private void CaptureKeyframe()
    {
        SetPlaying(false);
        var motion = Scene.Motion;
        var beat = motion.Quantize(_previewBeat);
        var pose = _poseEditing
            ? new StudioCameraPose(_draftPose.Camera, _draftPose.X, _draftPose.Y, _draftPose.Z)
            : SamplePreviewPose();
        try
        {
            motion.SetKey(new StudioCameraKeyframe { Beat = beat, Camera = pose.Camera with { }, X = pose.X, Y = pose.Y, Z = pose.Z });
            _selectedKey = motion.Keyframes.FindIndex(key => Math.Abs(key.Beat - beat) < .000001);
            _previewBeat = beat;
            _message = $"Keyframe at beat {beat.ToString("0.###", CultureInfo.InvariantCulture)}";
            BuildSettings();
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            _message = error.Message;
        }
        InvalidateCanvas(sceneChanged: true);
    }

    private void DeleteKeyframe()
    {
        var keys = Scene.Motion.Keyframes;
        if ((uint)_selectedKey >= keys.Count) return;
        keys.RemoveAt(_selectedKey);
        _selectedKey = -1;
        _poseEditing = false;
        BuildSettings();
        InvalidateCanvas(sceneChanged: true);
    }

    private StudioCameraPose SamplePreviewPose()
    {
        var motion = Scene.Motion;
        var beat = CurrentPreviewBeat() - motion.PhaseOffsetBeats;
        return motion.Sample(Scene.Camera, beat * 60 / motion.Tempo, new StudioClock(motion.Tempo, beat));
    }

    private (StudioScene Scene, double Seconds, StudioClock Clock) PreviewFrameState(double now)
    {
        var scene = RenderableScene();
        var motion = scene.Motion;
        if (motion.Mode == StudioCameraMode.Static)
            return (scene, now, default);

        if (_poseEditing)
        {
            var staticPose = new StudioCameraMotion
            {
                Mode = StudioCameraMode.Keyframes,
                Keyframes = [new StudioCameraKeyframe
                {
                    Beat = 0, Camera = _draftPose.Camera, X = _draftPose.X, Y = _draftPose.Y, Z = _draftPose.Z
                }]
            };
            return (scene with { Motion = staticPose }, 0, new StudioClock(120, 0));
        }
        var beat = CurrentPreviewBeat() - motion.PhaseOffsetBeats;
        return (scene, beat * 60 / motion.Tempo, new StudioClock(motion.Tempo, beat));
    }
}
