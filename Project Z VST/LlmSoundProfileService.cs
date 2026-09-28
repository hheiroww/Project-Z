using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Text.Json;
#if SOCKETJACK_LLM_RUNTIME
using LlmRuntime;
#endif

namespace ProjectZ.Vst;

internal sealed record LlmProfileResult(GranularProfile Profile, string Status);

internal sealed class LlmSoundProfileService : IDisposable
{
#if SOCKETJACK_LLM_RUNTIME
    private readonly Lazy<LlmModelRegistry> _registry = new(CreateRegistry, LazyThreadSafetyMode.ExecutionAndPublication);
#endif

    public async Task<LlmProfileResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Enter a sound description first.", nameof(prompt));

#if SOCKETJACK_LLM_RUNTIME
        try
        {
            var registry = _registry.Value;
            var configuredModel = Environment.GetEnvironmentVariable("PROJECTZ_LLM_MODEL");
            var model = !string.IsNullOrWhiteSpace(configuredModel)
                ? configuredModel
                : registry.ListModels().FirstOrDefault(item => item.Type.Equals("llm", StringComparison.OrdinalIgnoreCase))?.Key;
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("No SocketJack LlmRuntime model was found. Set PROJECTZ_LLM_MODEL or add a model to the runtime model folder.");

            var result = await registry.CompleteChatAsync(new LlmChatRequest
            {
                Model = model,
                MaxTokens = 1400,
                MaxTokensSpecified = true,
                Temperature = 0.55f,
                TopP = 0.9f,
                Messages =
                [
                    new LlmChatMessage("system", SystemPrompt),
                    new LlmChatMessage("user", prompt)
                ]
            }, cancellationToken).ConfigureAwait(false);

            return new LlmProfileResult(ParseProfile(result.Content), $"SocketJack LlmRuntime: {result.Model}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var fallback = CreateDeterministicProfile(prompt);
            return new LlmProfileResult(fallback, $"Local prompt profile used - LlmRuntime unavailable: {ex.Message}");
        }
#else
        await Task.Yield();
        return new LlmProfileResult(CreateDeterministicProfile(prompt), "Local prompt profile used - build beside SocketJack to enable LlmRuntime");
#endif
    }

    private static GranularProfile ParseProfile(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidDataException("The model did not return a JSON sound profile.");

        using var document = JsonDocument.Parse(response[start..(end + 1)]);
        var root = document.RootElement;
        var values = root.GetProperty("wavetable").EnumerateArray()
            .Select(value => Math.Clamp(value.GetSingle(), -1, 1)).ToArray();
        if (values.Length < 8)
            throw new InvalidDataException("The model wavetable needs at least eight points.");
        var table = ProfileFactory.Resample(values);
        return new GranularProfile(
            ReadString(root, "name", "LLM profile"), table, ProfileFactory.MakeSource(table),
            ReadNumber(root, "grainSizeMs", 85, 5, 500), ReadNumber(root, "density", 22, 1, 120),
            ReadNumber(root, "position", 50, 0, 100), ReadNumber(root, "spray", 28, 0, 100),
            ReadNumber(root, "pitchSemitones", 0, -24, 24), ReadNumber(root, "tableDepth", 55, 0, 100),
            ReadNumber(root, "stereoSpread", 65, 0, 100), ReadNumber(root, "mix", 100, 0, 100),
            ReadNumber(root, "outputDb", -2, -24, 12));
    }

    private static GranularProfile CreateDeterministicProfile(string prompt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(prompt));
        var harmonics = bytes.Take(12).Select((value, index) => ((value / 255f) * 2 - 1) / (index + 1)).ToArray();
        var table = new float[GranularProfile.TableLength];
        for (var i = 0; i < table.Length; i++)
        {
            var phase = Math.Tau * i / table.Length;
            for (var harmonic = 0; harmonic < harmonics.Length; harmonic++)
                table[i] += harmonics[harmonic] * (float)Math.Sin(phase * (harmonic + 1));
        }
        ProfileFactory.Normalize(table);
        return new GranularProfile(
            prompt.Length > 30 ? prompt[..30] + "..." : prompt,
            table, ProfileFactory.MakeSource(table),
            35 + bytes[12] / 255d * 180, 8 + bytes[13] / 255d * 52, bytes[14] / 2.55,
            bytes[15] / 2.55, (bytes[16] / 255d * 24) - 12, 30 + bytes[17] / 255d * 70,
            bytes[18] / 2.55, 100, -3);
    }

    private static string ReadString(JsonElement root, string name, string fallback) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static double ReadNumber(JsonElement root, string name, double fallback, double min, double max) =>
        root.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? Math.Clamp(number, min, max) : fallback;

#if SOCKETJACK_LLM_RUNTIME
    private static LlmModelRegistry CreateRegistry()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modelRoot = Environment.GetEnvironmentVariable("PROJECTZ_LLM_MODEL_ROOT")
            ?? Environment.GetEnvironmentVariable("JACKLLM_MODEL_ROOT");
        if (string.IsNullOrWhiteSpace(modelRoot))
            modelRoot = Path.Combine(localData, "JackLLM", "Models");
        var completeModelRoot = Environment.GetEnvironmentVariable("PROJECTZ_LLM_COMPLETE_MODEL_ROOT")
            ?? Environment.GetEnvironmentVariable("JACKLLM_COMPLETE_MODEL_ROOT");
        if (string.IsNullOrWhiteSpace(completeModelRoot))
            completeModelRoot = Path.Combine(localData, "JackLLM", "CompleteModels");
        return new LlmModelRegistry(new LlmRuntimeOptions
        {
            ModelRoot = modelRoot,
            CompleteModelRoot = completeModelRoot,
            IncludeLmStudioModels = true,
            DefaultBackend = LlmBackendKind.Auto,
            AllowBackendFallback = true,
            RequireGpuForAutoBackend = false,
            PreventCpuBackendFallback = false,
            DefaultWorkspaceRoot = Environment.CurrentDirectory
        });
    }
#endif

    public void Dispose()
    {
#if SOCKETJACK_LLM_RUNTIME
        if (_registry.IsValueCreated)
            _registry.Value.Dispose();
#endif
    }

    private const string SystemPrompt = """
        You design expressive granular-synthesis profiles. Return JSON only, with no markdown.
        Schema: {"name":"short name","grainSizeMs":85,"density":22,"position":50,"spray":28,
        "pitchSemitones":0,"tableDepth":55,"stereoSpread":65,"mix":100,"outputDb":-2,
        "wavetable":[64 floating point values between -1 and 1]}.
        Respect these ranges: grainSizeMs 5..500, density 1..120, position/spray/tableDepth/stereoSpread/mix 0..100,
        pitchSemitones -24..24, outputDb -24..12. Make the wavetable periodic and sonically related to the description.
        """;
}
