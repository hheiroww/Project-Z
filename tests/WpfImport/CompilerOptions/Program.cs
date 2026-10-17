namespace CompilerOptionsFixture;
// The removed SDK global using would make Timer ambiguous.
sealed class TimerProbe { public Timer? Timer { get; set; } }

class Program
{
    static async Task Main()
    {
        if (IntPtr.Size != 8) throw new Exception();
        var numbers = new List<int> { 1, 2, 3 };
        var sum = await Task.Run(() => numbers.Sum());
        if (sum != 6) throw new Exception();
        if (Abs(-2) != 2 || PathAlias.GetFileName("fixture.txt") != "fixture.txt") throw new Exception();
        Console.WriteLine("COMPILER_OPTIONS_OK");
    }
}
