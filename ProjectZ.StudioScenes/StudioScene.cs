namespace ProjectZ.StudioScenes;

public enum StudioSceneKind { Terrain = 0, Model = 1, TerrainAndModel = 2 }

public sealed record StudioScene
{
    public int Version { get; set; } = 1;
    public StudioSceneKind Kind { get; set; }
    public StudioCamera Camera { get; set; } = new();
    public StudioCameraMotion Motion { get; set; } = new();
    public StudioTerrain Terrain { get; set; } = new();
    public StudioSky Sky { get; set; } = new();
    public StudioModel Model { get; set; } = new();

    public bool HasTerrain => Kind is StudioSceneKind.Terrain or StudioSceneKind.TerrainAndModel;
    public bool HasModel => Kind is StudioSceneKind.Model or StudioSceneKind.TerrainAndModel;

    public StudioScene Copy() => this with
    {
        Camera = Camera with { }, Motion = Motion.Copy(), Terrain = Terrain with { Material = Terrain.Material with { } }, Sky = Sky with { },
        Model = Model with { Ik = Model.Ik is { } chain ? chain with { } : null }
    };

    public void Validate()
    {
        if (Version != 1 || !Enum.IsDefined(Kind) || Camera is null || Terrain is null || Model is null || Sky is null || Motion is null)
            throw new InvalidDataException("This Project-Z scene format or scene kind is unsupported.");

        SceneRanges.Check(Camera.Yaw, -3600, 3600, "Camera yaw");
        SceneRanges.Check(Camera.Pitch, -89, 89, "Camera pitch");
        SceneRanges.Check(Camera.Roll, -3600, 3600, "Camera roll");
        SceneRanges.Check(Camera.Zoom, .2f, 3f, "Camera zoom");
        SceneRanges.Check(Camera.FieldOfView, 18, 90, "Camera field of view");
        Motion.Validate();

        if (Terrain.Grid is < StudioTerrain.MinimumGridSize or > StudioTerrain.MaximumGridSize)
            throw new InvalidDataException("Terrain grid size must be between 9 and 257.");
        SceneRanges.Check(Terrain.Frequency, .25f, 12f, "Terrain frequency");
        SceneRanges.Check(Terrain.Relief, 0, 2, "Terrain relief");
        SceneRanges.Check(Terrain.Roughness, 0, 1, "Terrain roughness");
        SceneRanges.Check(Terrain.Ridge, 0, 1, "Terrain ridge");
        SceneRanges.Check(Terrain.WaterLevel, 0, 1, "Terrain water level");
        if (Terrain.Material is null)
            throw new InvalidDataException("The terrain material is missing.");
        Terrain.Material.Validate();

        if (Model.Animation is < -1 or > 16384)
            throw new InvalidDataException("The model animation index is invalid.");
        SceneRanges.Check(Model.Speed, .05f, 10, "Model speed");
        SceneRanges.Check(Model.RotationX, -3600, 3600, "Model rotation X");
        SceneRanges.Check(Model.RotationY, -3600, 3600, "Model rotation Y");
        SceneRanges.Check(Model.RotationZ, -3600, 3600, "Model rotation Z");
        SceneRanges.Check(Model.Scale, .025f, 8, "Model scale");
        SceneRanges.Check(Model.OffsetX, -10, 10, "Model offset X");
        SceneRanges.Check(Model.OffsetY, -10, 10, "Model offset Y");
        SceneRanges.Check(Model.OffsetZ, -10, 10, "Model offset Z");
        if (Model.Ik is { } chain)
            chain.Validate();
    }
}

public sealed record StudioCamera
{
    public float Yaw { get; set; } = 25;
    public float Pitch { get; set; } = 31;
    public float Roll { get; set; }
    public float Zoom { get; set; } = 1;
    public float FieldOfView { get; set; } = 45;
}

public sealed record StudioSky
{
    public bool Enabled { get; set; }
    public int TopColor { get; set; } = unchecked((int)0xff0e213f);
    public int HorizonColor { get; set; } = unchecked((int)0xff6a95b5);
}

public sealed record StudioTerrain
{
    public const int MinimumGridSize = 9;
    public const int MaximumGridSize = 257;

    public bool Endless { get; set; }
    public int Seed { get; set; } = 173;
    public int Grid { get; set; } = 65;
    public float Frequency { get; set; } = 2.2f;
    public float Relief { get; set; } = .62f;
    public float Roughness { get; set; } = .48f;
    public float Ridge { get; set; } = .32f;
    public float WaterLevel { get; set; } = .28f;
    public bool ShowWater { get; set; } = true;
    public int LowColor { get; set; } = unchecked((int)0xff264a50);
    public int HighColor { get; set; } = unchecked((int)0xff897360);
    public int SnowColor { get; set; } = unchecked((int)0xffe2eaf2);
    public int WaterColor { get; set; } = unchecked((int)0xc82c8495);
    public StudioTerrainMaterial Material { get; set; } = new();
}

public sealed record StudioModel
{
    public string Asset { get; set; } = "";
    public int Animation { get; set; } = -1;
    public float Speed { get; set; } = 1;
    public bool Loop { get; set; } = true;
    public float RotationX { get; set; } = -10;
    public float RotationY { get; set; } = 20;
    public float RotationZ { get; set; }
    public float Scale { get; set; } = 1;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }
    public StudioIk? Ik { get; set; }
}

public sealed record StudioIk
{
    public int Root { get; set; }
    public int Bend { get; set; } = 1;
    public int Tip { get; set; } = 2;
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetZ { get; set; }
    public float PoleX { get; set; }
    public float PoleY { get; set; } = 1;
    public float PoleZ { get; set; }
    public float Weight { get; set; } = 1;

    internal void Validate()
    {
        if (Root < 0 || Bend <= Root || Tip <= Bend || Tip >= 16384)
            throw new InvalidDataException("The IK chain must contain three distinct, ordered joints.");
        SceneRanges.Check(TargetX, -1000000, 1000000, "IK target X");
        SceneRanges.Check(TargetY, -1000000, 1000000, "IK target Y");
        SceneRanges.Check(TargetZ, -1000000, 1000000, "IK target Z");
        SceneRanges.Check(PoleX, -1000000, 1000000, "IK pole X");
        SceneRanges.Check(PoleY, -1000000, 1000000, "IK pole Y");
        SceneRanges.Check(PoleZ, -1000000, 1000000, "IK pole Z");
        SceneRanges.Check(Weight, 0, 1, "IK weight");
    }
}

public sealed record StudioJoint(string Name, int Parent);

public readonly record struct StudioSignal(float Bass = 0, float Mid = 0, float Treble = 0, float Beat = 0);

internal static class SceneRanges
{
    internal static void Check(float value, float minimum, float maximum, string label)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
            throw new InvalidDataException($"{label} must be between {minimum} and {maximum}.");
    }
}
