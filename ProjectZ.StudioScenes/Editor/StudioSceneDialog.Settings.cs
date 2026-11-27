namespace ProjectZ.StudioScenes;

public sealed partial class StudioSceneDialog
{
    private static readonly string[] PageNames = ["Scene", "Terrain", "Materials", "Model", "Rig", "Path"];
    private readonly int[] _scrollRows = new int[PageNames.Length];
    private EditorRow[][] _rows = [];
    private int _page;
    private int _terrainPreset;
    private int _materialPreset;
    private StudioIk? _draftChain;

    private abstract record EditorRow(string Label);

    private sealed record NumberRow(string Label, Func<float> Value, Action<float> Set,
        float Minimum, float Maximum, float Step) : EditorRow(Label);

    private sealed record ChoiceRow(string Label, string[] Options, Func<int> Selected, Action<int> Select) : EditorRow(Label);

    private sealed record ToggleRow(string Label, Func<bool> Value, Action<bool> Set) : EditorRow(Label);

    private sealed record SwatchRow(string Label, Func<int> Value, Action<int> Set) : EditorRow(Label);

    private sealed record TextRow(string Label, Func<string> Value) : EditorRow(Label);

    private sealed record ActionRow(string Label, Func<string> Caption, Action Invoke, Func<bool>? Enabled = null) : EditorRow(Label);

    private void BuildSettings()
    {
        var scene = Scene;
        var terrain = scene.Terrain;
        var material = terrain.Material;
        var model = scene.Model;
        var animations = _renderer?.Animations ?? Array.Empty<string>();

        _rows =
        [
            [
                new ChoiceRow("Contents", ["Terrain", "3D model", "Terrain and model", "Open ocean"], () => (int)scene.Kind, SetSceneKind),
                new NumberRow("Camera yaw", () => InputCamera.Yaw, value => InputCamera.Yaw = value, -3600, 3600, 1),
                new NumberRow("Camera pitch", () => InputCamera.Pitch, value => InputCamera.Pitch = value, -89, 89, 1),
                new NumberRow("Camera roll", () => InputCamera.Roll, value => InputCamera.Roll = value, -3600, 3600, 1),
                new NumberRow("Camera zoom", () => InputCamera.Zoom, value => InputCamera.Zoom = value, .2f, 3, .05f),
                new NumberRow("Field of view", () => InputCamera.FieldOfView, value => InputCamera.FieldOfView = value, 18, 90, 1),
                new ToggleRow("Show sky", () => scene.Sky.Enabled, enabled => scene.Sky.Enabled = enabled),
                new SwatchRow("Sky color", () => scene.Sky.TopColor, color => scene.Sky.TopColor = color),
                new SwatchRow("Horizon color", () => scene.Sky.HorizonColor, color => scene.Sky.HorizonColor = color)
            ],
            [
                new ChoiceRow("Terrain preset", ["Custom", "Alpine", "Desert", "Island"], () => _terrainPreset, SetTerrainPreset),
                new ChoiceRow("Quality", ["Custom", "Draft", "Balanced", "High", "Ultra"],
                    () => TerrainQuality(terrain), value => SetTerrainQuality(terrain, value)),
                new NumberRow("Seed", () => terrain.Seed, value => terrain.Seed = (int)value, -1000000, 1000000, 1),
                new NumberRow("Mesh resolution", () => terrain.Grid, value => terrain.Grid = (int)value, 9, 257, 8),
                new NumberRow("Frequency", () => terrain.Frequency, value => terrain.Frequency = value, .25f, 12, .05f),
                new NumberRow("Relief", () => terrain.Relief, value => terrain.Relief = value, 0, 2, .05f),
                new NumberRow("Roughness", () => terrain.Roughness, value => terrain.Roughness = value, 0, 1, .05f),
                new NumberRow("Ridges", () => terrain.Ridge, value => terrain.Ridge = value, 0, 1, .05f),
                new ToggleRow("Endless terrain", () => terrain.Endless, value => terrain.Endless = value),
                new ToggleRow("Show water", () => terrain.ShowWater, value => terrain.ShowWater = value),
                new NumberRow("Water level", () => terrain.WaterLevel, value => terrain.WaterLevel = value, 0, 1, .02f),
                new SwatchRow("Low elevation", () => terrain.LowColor, value => terrain.LowColor = value),
                new SwatchRow("High elevation", () => terrain.HighColor, value => terrain.HighColor = value),
                new SwatchRow("Water color", () => terrain.WaterColor, value => terrain.WaterColor = value)
            ],
            [
                new ToggleRow("Procedural texture", () => material.Enabled, value => material.Enabled = value),
                new ChoiceRow("Altitude preset", ["Custom", .. Enum.GetNames<StudioTerrainAltitudePreset>()],
                    () => _materialPreset, value => SetAltitudePreset(material, value)),
                new NumberRow("Texture resolution", () => material.TextureResolution, value => material.TextureResolution = (int)value,
                    StudioTerrainMaterial.MinimumTextureResolution, StudioTerrainMaterial.MaximumTextureResolution, 64),
                new NumberRow("Texture tiling", () => material.TextureScale, value => material.TextureScale = value, .25f, 8, .05f),
                new NumberRow("Texture strength", () => material.TextureStrength, value => material.TextureStrength = value, 0, 1, .05f),
                new SwatchRow("Sand color", () => material.SandColor, value => material.SandColor = value),
                new SwatchRow("Soil color", () => material.SoilColor, value => material.SoilColor = value),
                new SwatchRow("Grass color", () => material.GrassColor, value => material.GrassColor = value),
                new SwatchRow("Rock color", () => material.RockColor, value => material.RockColor = value),
                new NumberRow("Sand altitude", () => material.SandAltitude, value => material.SandAltitude = value, 0, 1, .025f),
                new NumberRow("Grass altitude", () => material.GrassAltitude, value => material.GrassAltitude = value, 0, 1, .025f),
                new NumberRow("Rock slope", () => material.RockSlope, value => material.RockSlope = value, 0, 1, .025f),
                new NumberRow("Altitude blend", () => material.AltitudeBlend, value => material.AltitudeBlend = value, .005f, .4f, .01f),
                new ToggleRow("Show snow", () => material.SnowEnabled, value => material.SnowEnabled = value),
                new SwatchRow("Snow color", () => terrain.SnowColor, value => terrain.SnowColor = value),
                new NumberRow("Snow altitude", () => material.SnowAltitude, value => material.SnowAltitude = value, 0, 1, .025f),
                new NumberRow("Snow slope", () => material.SnowSlope, value => material.SnowSlope = value, 0, 1, .025f),
                new NumberRow("Snow blend", () => material.SnowBlend, value => material.SnowBlend = value, .005f, .4f, .01f)
            ],
            [
                new TextRow("Asset", () => ModelPath is null ? "Choose a model" : Path.GetFileName(ModelPath)),
                new ChoiceRow("Animation", ["Rest pose", .. animations], () => model.Animation + 1, value => model.Animation = value - 1),
                new NumberRow("Playback speed", () => model.Speed, value => model.Speed = value, .05f, 10, .05f),
                new ToggleRow("Loop animation", () => model.Loop, value => model.Loop = value),
                new NumberRow("Rotate X", () => model.RotationX, value => model.RotationX = value, -3600, 3600, 1),
                new NumberRow("Rotate Y", () => model.RotationY, value => model.RotationY = value, -3600, 3600, 1),
                new NumberRow("Rotate Z", () => model.RotationZ, value => model.RotationZ = value, -3600, 3600, 1),
                new NumberRow("Scale", () => model.Scale, value => model.Scale = value, .025f, 8, .05f),
                new NumberRow("Offset X", () => model.OffsetX, value => model.OffsetX = value, -10, 10, .05f),
                new NumberRow("Offset Y", () => model.OffsetY, value => model.OffsetY = value, -10, 10, .05f),
                new NumberRow("Offset Z", () => model.OffsetZ, value => model.OffsetZ = value, -10, 10, .05f)
            ],
            BuildRig(model),
            BuildPath()
        ];
        if (scene.HasOcean)
            _rows[1] = BuildOcean(scene.Ocean);
        _popup = null;
        _numberEdit = null;
    }

    private static EditorRow[] BuildOcean(StudioOcean ocean) =>
    [
        new NumberRow("Seed", () => ocean.Seed, value => ocean.Seed = (int)value, -1000000, 1000000, 1),
        new ChoiceRow("Wave resolution", ["64", "128", "256"], () => ocean.FftResolution switch { 64 => 0, 256 => 2, _ => 1 },
            index => ocean.FftResolution = 64 << index),
        new NumberRow("Wind speed (m/s)", () => ocean.WindSpeed, value => ocean.WindSpeed = value, 1, 35, .5f),
        new NumberRow("Wind direction", () => ocean.WindDirection, value => ocean.WindDirection = value, -3600, 3600, 2),
        new NumberRow("Wind fetch (m)", () => ocean.Fetch, value => ocean.Fetch = value, 100, 1000000, 500),
        new NumberRow("Depth (m)", () => ocean.Depth, value => ocean.Depth = value, 1, 1000, 2),
        new NumberRow("Swell", () => ocean.Swell, value => ocean.Swell = value, 0, 1, .025f),
        new NumberRow("Wave scale", () => ocean.WaveScale, value => ocean.WaveScale = value, 0, 3, .05f),
        new NumberRow("Choppiness", () => ocean.Choppiness, value => ocean.Choppiness = value, 0, 3, .05f),
        new NumberRow("Foam", () => ocean.Foam, value => ocean.Foam = value, 0, 2, .05f),
        new NumberRow("Roughness", () => ocean.Roughness, value => ocean.Roughness = value, .025f, .75f, .02f),
        new NumberRow("Audio response", () => ocean.AudioResponse, value => ocean.AudioResponse = value, 0, 2, .05f),
        new NumberRow("Eye height (m)", () => ocean.EyeHeight, value => ocean.EyeHeight = value, .5f, 150, .2f),
        new NumberRow("Sun elevation", () => ocean.SunElevation, value => ocean.SunElevation = value, -10, 90, 1),
        new NumberRow("Sun azimuth", () => ocean.SunAzimuth, value => ocean.SunAzimuth = value, -3600, 3600, 1),
        new NumberRow("Sun intensity", () => ocean.SunIntensity, value => ocean.SunIntensity = value, 0, 3, .05f),
        new NumberRow("Cloud coverage", () => ocean.CloudCoverage, value => ocean.CloudCoverage = value, 0, 1, .025f),
        new NumberRow("Haze", () => ocean.Haze, value => ocean.Haze = value, 0, 2, .05f)
    ];

    private EditorRow[] BuildRig(StudioModel model)
    {
        var joints = _renderer?.Joints;
        if (joints is null || joints.Count < 3)
            return [new TextRow("Inverse kinematics", () => "Choose a skinned model")];

        var jointNames = joints.Select((joint, index) => $"[{index}] {joint.Name}").ToArray();
        var chain = model.Ik ?? (_draftChain ??= InitialChain(joints));
        return
        [
            new ToggleRow("Two-bone IK", () => model.Ik is not null, value => model.Ik = value ? chain : null),
            new ChoiceRow("Root joint", jointNames, () => chain.Root, value => chain.Root = value),
            new ChoiceRow("Bend joint", jointNames, () => chain.Bend, value => chain.Bend = value),
            new ChoiceRow("Tip joint", jointNames, () => chain.Tip, value => chain.Tip = value),
            new NumberRow("Target X", () => chain.TargetX, value => chain.TargetX = value, -100, 100, .05f),
            new NumberRow("Target Y", () => chain.TargetY, value => chain.TargetY = value, -100, 100, .05f),
            new NumberRow("Target Z", () => chain.TargetZ, value => chain.TargetZ = value, -100, 100, .05f),
            new NumberRow("Pole X", () => chain.PoleX, value => chain.PoleX = value, -100, 100, .05f),
            new NumberRow("Pole Y", () => chain.PoleY, value => chain.PoleY = value, -100, 100, .05f),
            new NumberRow("Pole Z", () => chain.PoleZ, value => chain.PoleZ = value, -100, 100, .05f),
            new NumberRow("IK weight", () => chain.Weight, value => chain.Weight = value, 0, 1, .05f)
        ];
    }

    private static StudioIk InitialChain(IReadOnlyList<StudioJoint> joints)
    {
        for (var tip = 0; tip < joints.Count; tip++)
        {
            var bend = joints[tip].Parent;
            if ((uint)bend < joints.Count && (uint)joints[bend].Parent < joints.Count)
                return new StudioIk { Root = joints[bend].Parent, Bend = bend, Tip = tip };
        }
        return new StudioIk();
    }

    private void SetSceneKind(int index)
    {
        var wanted = (StudioSceneKind)index;
        if (Scene.Kind == wanted) return;
        if (wanted == StudioSceneKind.Ocean && Scene.Camera == new StudioCamera() && Scene.Sky == new StudioSky())
        {
            var defaults = StudioScene.CreateOcean();
            Scene.Camera = defaults.Camera;
            Scene.Sky = defaults.Sky;
        }
        Scene.Kind = wanted;
        LoadRenderer();
    }

    private void SetTerrainPreset(int index)
    {
        _terrainPreset = index;
        _materialPreset = 0;
        if (index == 0) return;
        Scene.Terrain = index switch
        {
            1 => new() { Seed = 173, Relief = .82f, Ridge = .67f, WaterLevel = .24f },
            2 => new() { Seed = 824, Relief = .43f, Roughness = .59f, Ridge = .38f, ShowWater = false,
                LowColor = unchecked((int)0xffa85037), HighColor = unchecked((int)0xffd39f64), SnowColor = unchecked((int)0xffffd5a3) },
            _ => new() { Seed = 916, Relief = .56f, Roughness = .35f, Frequency = 1.8f, WaterLevel = .35f,
                LowColor = unchecked((int)0xff489177), HighColor = unchecked((int)0xff728855), WaterColor = unchecked((int)0xc8236482) }
        };
        var chosen = index switch { 1 => StudioTerrainAltitudePreset.Alpine, 2 => StudioTerrainAltitudePreset.Arid, _ => StudioTerrainAltitudePreset.Coastal };
        Scene.Terrain.Material.ApplyAltitudePreset(chosen);
        _materialPreset = (int)chosen + 1;
        Scene.Sky = index switch
        {
            2 => new() { Enabled = true, TopColor = unchecked((int)0xff493044), HorizonColor = unchecked((int)0xffdf9273) },
            3 => new() { Enabled = true, TopColor = unchecked((int)0xff164c7e), HorizonColor = unchecked((int)0xffa1c6ca) },
            _ => new() { Enabled = true }
        };
        BuildSettings();
    }

    private static int TerrainQuality(StudioTerrain terrain) => (terrain.Grid, terrain.Material.TextureResolution) switch
    {
        (33, 128) => 1, (65, 256) => 2, (129, 512) => 3, (257, 1024) => 4, _ => 0
    };

    private static void SetTerrainQuality(StudioTerrain terrain, int index)
    {
        if (index == 0) return;
        (terrain.Grid, terrain.Material.TextureResolution) = index switch
        {
            1 => (33, 128), 2 => (65, 256), 3 => (129, 512), _ => (257, 1024)
        };
    }

    private void SetAltitudePreset(StudioTerrainMaterial material, int index)
    {
        _materialPreset = index;
        _terrainPreset = 0;
        if (index > 0)
            material.ApplyAltitudePreset((StudioTerrainAltitudePreset)(index - 1));
    }
}
