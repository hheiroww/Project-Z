using System.Windows.Input;
namespace ProjectZ.WpfCompatibility.Input;

public sealed class Cursor
{
    internal System.Windows.Forms.Cursor Native { get; }
    internal Cursor(System.Windows.Forms.Cursor native) => Native = native;
}
public static class Cursors
{
    public static Cursor Arrow { get; } = new(System.Windows.Forms.Cursors.Arrow);
    public static Cursor Hand { get; } = new(System.Windows.Forms.Cursors.Hand);
    public static Cursor IBeam { get; } = new(System.Windows.Forms.Cursors.IBeam);
    public static Cursor Wait { get; } = new(System.Windows.Forms.Cursors.WaitCursor);
    public static Cursor SizeWE { get; } = new(System.Windows.Forms.Cursors.SizeWE);
    public static Cursor SizeNS { get; } = new(System.Windows.Forms.Cursors.SizeNS);
    public static Cursor SizeAll { get; } = new(System.Windows.Forms.Cursors.SizeAll);
    public static Cursor SizeNWSE { get; } = new(System.Windows.Forms.Cursors.SizeNWSE);
    public static Cursor SizeNESW { get; } = new(System.Windows.Forms.Cursors.SizeNESW);
    public static Cursor Cross { get; } = new(System.Windows.Forms.Cursors.Cross);
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
