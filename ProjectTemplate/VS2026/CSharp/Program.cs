namespace $safeprojectname$;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var game = new GameWindow();
        game.Run();
    }
}
