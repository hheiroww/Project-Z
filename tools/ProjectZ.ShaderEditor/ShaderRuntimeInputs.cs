namespace ProjectZ.ShaderEditor;

/// <summary>Scalar values supplied by a real-time shader host for each frame.</summary>
public static class ShaderRuntimeInputs
{
    public static IReadOnlyList<string> ScalarNames { get; } = Array.AsReadOnly<string>(
    [
        "AudioLevel", "AudioLow", "AudioMid", "AudioHigh", "AudioBeat", "BeatPhase", "Tempo",
        "Macro01", "Macro02", "Macro03", "Macro04", "Macro05", "Macro06", "Macro07", "Macro08",
        "Macro09", "Macro10", "Macro11", "Macro12", "Macro13", "Macro14", "Macro15", "Macro16"
    ]);

    internal static string Label(string name) => name switch
    {
        "AudioLevel" => "Audio level",
        "AudioLow" => "Low frequencies",
        "AudioMid" => "Mid frequencies",
        "AudioHigh" => "High frequencies",
        "AudioBeat" => "Beat strength",
        "BeatPhase" => "Beat phase",
        "Tempo" => "Tempo (BPM)",
        _ => $"Macro {name[5..]}"
    };
}
