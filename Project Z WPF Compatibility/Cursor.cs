using System.Windows.Input;
using ProjectZ.Shared.Drawing;
namespace ProjectZ.WpfCompatibility.Input;

public sealed class Cursor
{
    internal SceneCursor Shape { get; }
    internal Cursor(SceneCursor shape) => Shape = shape;
}
public static class Cursors
{
    public static Cursor Arrow { get; } = new(SceneCursor.Arrow);
    public static Cursor Hand { get; } = new(SceneCursor.Hand);
    public static Cursor IBeam { get; } = new(SceneCursor.IBeam);
    public static Cursor Wait { get; } = new(SceneCursor.Wait);
    public static Cursor SizeWE { get; } = new(SceneCursor.SizeWE);
    public static Cursor SizeNS { get; } = new(SceneCursor.SizeNS);
    public static Cursor SizeAll { get; } = new(SceneCursor.SizeAll);
    public static Cursor SizeNWSE { get; } = new(SceneCursor.SizeNWSE);
    public static Cursor SizeNESW { get; } = new(SceneCursor.SizeNESW);
    public static Cursor Cross { get; } = new(SceneCursor.Cross);

    internal static Cursor? FromShape(SceneCursor? shape) => shape switch
    {
        SceneCursor.Arrow => Arrow,
        SceneCursor.Hand => Hand,
        SceneCursor.IBeam => IBeam,
        SceneCursor.Wait => Wait,
        SceneCursor.SizeWE => SizeWE,
        SceneCursor.SizeNS => SizeNS,
        SceneCursor.SizeAll => SizeAll,
        SceneCursor.SizeNWSE => SizeNWSE,
        SceneCursor.SizeNESW => SizeNESW,
        SceneCursor.Cross => Cross,
        _ => null
    };
}
public static class ApplicationCommands
{
    sealed class CopyCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is UIElement element && element.GetCopyText().Length > 0;
        public void Execute(object? parameter) { if (parameter is UIElement element) element.Copy(); }
    }
    public static ICommand Copy { get; } = new CopyCommand();
}
