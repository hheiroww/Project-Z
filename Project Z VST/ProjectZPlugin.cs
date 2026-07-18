using AudioPlugSharp;
using AudioPlugSharpWPF;

namespace ProjectZ.Vst;

/// <summary>A stereo granular resynthesizer with Project Z's native DirectX 11 editor.</summary>
public sealed class ProjectZPlugin : AudioPluginWPF
{
    private readonly GranularEngine _engine = new();
    private readonly LlmSoundProfileService _llmProfiles = new();
    private FloatAudioIOPort? _stereoOutput;

    internal AudioPluginParameter GrainSizeParameter { get; private set; } = null!;
    internal AudioPluginParameter DensityParameter { get; private set; } = null!;
    internal AudioPluginParameter PositionParameter { get; private set; } = null!;
    internal AudioPluginParameter SprayParameter { get; private set; } = null!;
    internal AudioPluginParameter PitchParameter { get; private set; } = null!;
    internal AudioPluginParameter TableDepthParameter { get; private set; } = null!;
    internal AudioPluginParameter StereoSpreadParameter { get; private set; } = null!;
    internal AudioPluginParameter LevelParameter { get; private set; } = null!;
    internal AudioPluginParameter OutputParameter { get; private set; } = null!;
    internal AudioPluginParameter HoldParameter { get; private set; } = null!;
    internal AudioPluginParameter MuteParameter { get; private set; } = null!;
    internal string ProfileName => _engine.Profile.Name;

    public ProjectZPlugin()
    {
        Company = "Project Z";
        Website = "https://github.com/JackOfFates/Project-Z";
        Contact = "Project Z";
        PluginName = "Project Z Granulizer";
        PluginCategory = "Instrument|Synth";
        PluginVersion = "2.0.0";
        // New instrument UID: do not reuse the earlier effect prototype's UID.
        PluginID = 0x505A4752414E5331;
        HasUserInterface = true;
        EditorWidth = 1040;
        EditorHeight = 700;
        SampleFormatsSupported = EAudioBitsPerSample.Bits32;
    }

    public override void Initialize()
    {
        InputPorts = [];
        OutputPorts = [_stereoOutput = new FloatAudioIOPort("Stereo Output", EAudioChannelConfiguration.Stereo)];

        GrainSizeParameter = Add("grain-size", "Grain Size", 5, 500, 85, "{0:0} ms");
        DensityParameter = Add("density", "Density", 1, 120, 22, "{0:0}/s");
        PositionParameter = Add("position", "Position", 0, 100, 50, "{0:0}%");
        SprayParameter = Add("spray", "Spray", 0, 100, 28, "{0:0}%");
        PitchParameter = Add("pitch", "Pitch", -24, 24, 0, "{0:0.0} st");
        TableDepthParameter = Add("table-depth", "Table Depth", 0, 100, 55, "{0:0}%");
        StereoSpreadParameter = Add("stereo-spread", "Stereo Spread", 0, 100, 65, "{0:0}%");
        LevelParameter = Add("level", "Grain Level", 0, 100, 100, "{0:0}%");
        OutputParameter = Add("output", "Output", -24, 12, 0, "{0:0.0} dB");
        HoldParameter = Add("hold", "Hold Notes", 0, 1, 0, "{0:0}");
        MuteParameter = Add("mute", "Mute", 0, 1, 0, "{0:0}");
        base.Initialize();
        if (Host is not null)
            _engine.Configure(Host.SampleRate);
    }

    public override void InitializeProcessing()
    {
        base.InitializeProcessing();
        _engine.Configure(Host.SampleRate);
    }

    public override void HandleNoteOn(int channel, int noteNumber, float velocity, int sampleOffset) =>
        _engine.NoteOn(noteNumber, velocity);

    public override void HandleNoteOff(int channel, int noteNumber, float velocity, int sampleOffset)
    {
        if (HoldParameter.ProcessValue < 0.5)
            _engine.NoteOff(noteNumber);
    }

    public override void Stop()
    {
        _engine.AllNotesOff();
        base.Stop();
    }

    public override void Process()
    {
        base.Process();
        Host.ProcessAllEvents();
        if (_stereoOutput is null)
            return;

        Span<float> outputLeft = _stereoOutput.GetAudioBuffer(0);
        Span<float> outputRight = _stereoOutput.GetAudioBuffer(1);
        var currentSample = 0;
        var nextSample = 0;
        do
        {
            nextSample = Math.Clamp(Host.ProcessEvents(), currentSample, outputLeft.Length);
            if (nextSample > currentSample)
            {
                var settings = ReadSettings();
                _engine.Process(outputLeft[currentSample..nextSample], outputRight[currentSample..nextSample], settings);
            }
            currentSample = nextSample;
        }
        while (nextSample < outputLeft.Length);
    }

    internal Task<GranularProfile> ImportAudioAsync(string path, CancellationToken cancellationToken = default) =>
        ProfileImportService.FromAudioAsync(path, cancellationToken);

    internal Task<GranularProfile> ImportImageAsync(string path, CancellationToken cancellationToken = default) =>
        ProfileImportService.FromImageAsync(path, cancellationToken);

    internal Task<GranularProfile> ImportTextAsync(string path, CancellationToken cancellationToken = default) =>
        ProfileImportService.FromTextFileAsync(path, cancellationToken);

    internal Task<GranularProfile> ImportFileAsync(string path, CancellationToken cancellationToken = default) =>
        ProfileImportService.FromFileAsync(path, cancellationToken);

    internal Task<LlmProfileResult> GenerateProfileAsync(string prompt, CancellationToken cancellationToken = default) =>
        _llmProfiles.GenerateAsync(prompt, cancellationToken);

    internal void ApplyProfile(GranularProfile profile)
    {
        _engine.SetProfile(profile);
        GrainSizeParameter.EditValue = profile.GrainSizeMs;
        DensityParameter.EditValue = profile.Density;
        PositionParameter.EditValue = profile.Position;
        SprayParameter.EditValue = profile.Spray;
        PitchParameter.EditValue = profile.PitchSemitones;
        TableDepthParameter.EditValue = profile.TableDepth;
        StereoSpreadParameter.EditValue = profile.StereoSpread;
        LevelParameter.EditValue = profile.Mix;
        OutputParameter.EditValue = profile.OutputDb;
    }

    public override void RestoreState(byte[] stateData)
    {
        try
        {
            base.RestoreState(stateData);
        }
        catch (Exception ex)
        {
            // AudioPlugSharp 0.7.9 can throw when hosts supply malformed or
            // obsolete parameter XML. A bad preset must not escape into FL.
            Logger.Log("Project Z ignored incompatible saved state: " + ex);
            EditorWidth = 1040;
            EditorHeight = 700;
        }
    }

    private GranularSettings ReadSettings() => new(
        (float)GrainSizeParameter.ProcessValue, (float)DensityParameter.ProcessValue,
        (float)PositionParameter.ProcessValue, (float)SprayParameter.ProcessValue,
        (float)PitchParameter.ProcessValue, (float)TableDepthParameter.ProcessValue,
        (float)StereoSpreadParameter.ProcessValue, (float)LevelParameter.ProcessValue,
        (float)OutputParameter.ProcessValue, HoldParameter.ProcessValue >= 0.5,
        MuteParameter.ProcessValue >= 0.5);

    private AudioPluginParameter Add(string id, string name, double min, double max, double defaultValue, string format)
    {
        var parameter = new AudioPluginParameter
        {
            ID = id, Name = name, MinValue = min, MaxValue = max,
            DefaultValue = defaultValue, ValueFormat = format
        };
        AddParameter(parameter);
        return parameter;
    }

    public override System.Windows.Controls.UserControl GetEditorView() => new PluginEditorView(this);
}
