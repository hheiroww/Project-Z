using Microsoft.Xna.Framework;
using ProjectZ.Shared.Composition;

internal sealed class BackdropFixture : IDesktopCaptureSource
{
    public bool ExcludesHostWindow => true;
    public Rectangle WindowBounds => new(100, 100, 480, 320);
    public float DpiScale => 1;
    public bool TryCapture(Rectangle area, int downsample, byte[] rgba)
    {
        var width = (area.Width + downsample - 1) / downsample;
        var height = (area.Height + downsample - 1) / downsample;
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var p = (y * width + x) * 4;
            var left = area.X + x * downsample < 340;
            rgba[p] = left ? (byte)255 : (byte)0;
            rgba[p + 1] = 0;
            rgba[p + 2] = left ? (byte)0 : (byte)255;
            rgba[p + 3] = 255;
        }
        return true;
    }
    public void Dispose() { }
}
