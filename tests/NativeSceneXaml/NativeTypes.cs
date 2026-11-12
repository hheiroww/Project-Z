namespace Microsoft.Xna.Framework
{
    readonly record struct Vector2(float X, float Y);
    readonly record struct Vector3(float X, float Y, float Z);
}
namespace ProjectZ.Shared.Physics
{
    enum FrictionType { Custom, Geometry, Ice, Water, Snow, Sand, Rock, Sandpaper }
}
namespace ProjectZ.Shared.Drawing.UI
{
    enum PhysicsMotion { RenderTranslation, Position }
}
namespace ProjectZ.Shared.Drawing
{
    enum SceneCursor { Arrow, Hand, IBeam }
}
