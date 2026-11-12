using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectZ.ShaderEditor;

public enum ShaderMode { Simple, Advanced }

public enum ShaderValueType { Float, Float2, Float3, Float4 }

public sealed class ShaderNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Expression { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double[] Values { get; set; } = [];
}

public sealed class ShaderConnection
{
    public Guid Source { get; set; }
    public Guid Target { get; set; }
    public string Input { get; set; } = "";
}

public sealed class ShaderDocument
{
    private const int CurrentVersion = 1;
    public const double MaximumNodeCoordinate = 1_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = "Untitled";
    public ShaderMode Mode { get; set; }
    public List<ShaderNode> Nodes { get; set; } = [];
    public List<ShaderConnection> Connections { get; set; } = [];

    public static ShaderDocument Create()
    {
        var source = ShaderNodes.Create("Source", 100, 180);
        var output = ShaderNodes.Create("Output", 440, 180);
        return new ShaderDocument
        {
            Nodes = [source, output],
            Connections = [new ShaderConnection { Source = source.Id, Target = output.Id, Input = "Color" }]
        };
    }

    public static ShaderDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        var document = JsonSerializer.Deserialize<ShaderDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("The shader document is empty.");
        document.GetEvaluationOrder();
        return document;
    }

    public void Save(string path)
    {
        GetEvaluationOrder();
        string fullPath = Path.GetFullPath(path);
        string stagedPath = fullPath + $".{Guid.NewGuid():N}.writing";
        using (var stream = File.Open(stagedPath, FileMode.CreateNew))
            JsonSerializer.Serialize(stream, this, JsonOptions);
        File.Move(stagedPath, fullPath, overwrite: true);
    }

    public void Validate() => GetEvaluationOrder();

    internal List<ShaderNode> GetEvaluationOrder()
    {
        if (Version != CurrentVersion)
            throw new InvalidDataException($"Unsupported shader document version {Version}.");
        if (Name is null || !Enum.IsDefined(Mode) || Nodes is null || Connections is null)
            throw new InvalidDataException("The shader document is missing required fields.");

        var nodes = new Dictionary<Guid, ShaderNode>();
        var incoming = new Dictionary<Guid, int>();
        var outgoing = new Dictionary<Guid, List<Guid>>();
        int outputCount = 0;

        foreach (var node in Nodes)
        {
            if (node is null || node.Id == Guid.Empty || !nodes.TryAdd(node.Id, node))
                throw new InvalidDataException("Every shader node needs a unique ID.");
            var definition = ShaderNodes.Get(node.Kind);
            if (node.Name is null || node.Expression is null || !ValidCoordinate(node.X) || !ValidCoordinate(node.Y) ||
                node.Values is null || node.Values.Length != definition.Parameters.Count ||
                node.Values.Any(value => !double.IsFinite(value) || Math.Abs(value) > float.MaxValue))
                throw new InvalidDataException($"The {definition.Label} node has invalid properties.");
            if (node.Kind == "Output")
                outputCount++;
            incoming.Add(node.Id, 0);
            outgoing.Add(node.Id, []);
        }
        if (outputCount != 1)
            throw new InvalidDataException("The shader graph needs exactly one XAML Output node.");

        var occupiedPorts = new HashSet<(Guid Node, string Input)>();
        foreach (var wire in Connections)
        {
            if (wire is null || !nodes.TryGetValue(wire.Source, out var source) || !nodes.TryGetValue(wire.Target, out var target))
                throw new InvalidDataException("A shader connection references a missing node.");
            if (!ShaderNodes.PortsMatch(source, target, wire.Input))
                throw new InvalidDataException($"The {ShaderNodes.Get(source.Kind).Label} node cannot connect to {ShaderNodes.Get(target.Kind).Label}.{wire.Input}.");
            if (!occupiedPorts.Add((wire.Target, wire.Input)))
                throw new InvalidDataException("Each shader input can have only one connection.");
            incoming[wire.Target]++;
            outgoing[wire.Source].Add(wire.Target);
        }

        var ready = new Queue<ShaderNode>(Nodes.Where(node => incoming[node.Id] == 0));
        var ordered = new List<ShaderNode>(Nodes.Count);
        while (ready.TryDequeue(out var node))
        {
            ordered.Add(node);
            foreach (var targetId in outgoing[node.Id])
            {
                if (--incoming[targetId] == 0)
                    ready.Enqueue(nodes[targetId]);
            }
        }
        if (ordered.Count != Nodes.Count)
            throw new InvalidDataException("Shader connections cannot form a cycle.");
        return ordered;
    }

    private static bool ValidCoordinate(double value) => double.IsFinite(value) && Math.Abs(value) <= MaximumNodeCoordinate;
}

public sealed record ShaderPort(string Name, ShaderValueType Type)
{
    internal string DefaultExpression { get; init; } = "";
}

public sealed record ShaderParameter(string Name, double DefaultValue);

public sealed class ShaderNodeDefinition
{
    internal ShaderNodeDefinition(string kind, string label, string category, ShaderValueType? output,
        ShaderPort[] inputs, ShaderParameter[] parameters, Func<string[], string[], string>? expression,
        string? defaultCustomExpression = null)
    {
        Kind = kind;
        Label = label;
        Category = category;
        Output = output;
        Inputs = Array.AsReadOnly(inputs);
        Parameters = Array.AsReadOnly(parameters);
        Expression = expression;
        DefaultCustomExpression = defaultCustomExpression;
    }

    public string Kind { get; }
    public string Label { get; }
    public string Category { get; }
    public ShaderValueType? Output { get; }
    public IReadOnlyList<ShaderPort> Inputs { get; }
    public IReadOnlyList<ShaderParameter> Parameters { get; }
    public bool HasExpression => DefaultCustomExpression is not null;
    internal Func<string[], string[], string>? Expression { get; }
    internal string? DefaultCustomExpression { get; }
}
