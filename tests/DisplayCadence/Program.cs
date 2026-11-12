using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing;
using ProjectZ.Windows;
using Color = Microsoft.Xna.Framework.Color;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var expected = Enum.Parse<ProjectZGraphicsBackend>(args[0]);
        if (GraphicsBackendInfo.Current != expected)
            throw new InvalidOperationException("The probe loaded the wrong graphics backend.");

        bool tearingSupported = DisplayCadence.DxgiTearingSupported;
        if (tearingSupported && expected != ProjectZGraphicsBackend.DirectX12)
            throw new InvalidOperationException("The fallback backend reported DXGI tearing support.");

        using var game = new PresentationProbe();
        game.Run();
        Console.WriteLine($"PASS {expected}. DXGI tearing supported={tearingSupported}. Presented 8 frames in each mode, including resize.");
    }
}

internal sealed class PresentationProbe : Game
{
    private readonly GraphicsDeviceManager graphics;
    private int frames;

    public PresentationProbe()
    {
        Window.Title = "Project-Z display cadence check";
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 320,
            PreferredBackBufferHeight = 200,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        IsFixedTimeStep = true;
        InactiveSleepTime = TimeSpan.Zero;
        DisplayCadence.Apply(this, graphics, true, 75);
        if (graphics.GraphicsDevice is not null)
            throw new InvalidOperationException("Cadence setup created the graphics device early.");
    }

    protected override void Update(GameTime gameTime)
    {
        if (frames == 8 && graphics.SynchronizeWithVerticalRetrace)
        {
            DisplayCadence.Apply(this, graphics, false, 144);
            VerifyInterval(PresentInterval.One, 144);
            graphics.PreferredBackBufferWidth = 360;
            graphics.ApplyChanges();
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        VerifyInterval(frames < 8 ? PresentInterval.One : PresentInterval.Immediate, frames < 8 ? 75 : 144);
        if (frames >= 8 && GraphicsDevice.PresentationParameters.BackBufferWidth != 360)
            throw new InvalidOperationException("The swap chain resize did not apply.");
        GraphicsDevice.Clear(frames < 8 ? Color.SteelBlue : Color.SeaGreen);
        base.Draw(gameTime);
    }

    protected override void EndDraw()
    {
        base.EndDraw();
        if (++frames == 16)
            Exit();
    }

    private void VerifyInterval(PresentInterval expected, int framesPerSecond)
    {
        if (TargetElapsedTime != TimeSpan.FromSeconds(1.0 / framesPerSecond) ||
            GraphicsDevice.PresentationParameters.PresentationInterval != expected)
            throw new InvalidOperationException("The frame interval or swap chain presentation mode did not match.");
    }
}
