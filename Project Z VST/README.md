# Project Z Granulizer VST3

Project Z Granulizer is a MIDI-driven stereo granular synthesizer. Its editor is rendered
by Project Z through KNI's native DirectX 11 WinForms backend, embedded in the
DAW's WPF-owned VST3 editor window.

## Sound engine

- Up to 128 preallocated grains sourced from the built-in harmonic wave or an
  imported/generated sample profile.
- Automatable grain size, density, position, spray, pitch, wavetable depth,
  stereo spread, level, output, note hold, and mute controls.
- A 2048-point wavetable modulates grain duration and pitch independently for
  each new grain.
- Sound files become granular sources; images become luminance-derived tables;
  text becomes Unicode-derived tables and synthetic sources.
- File decoding, image processing, text processing, and LLM inference run away
  from the real-time audio callback. The finished immutable profile is swapped
  into the engine atomically.

## SocketJack LLM Runtime

When the sibling `SocketJack/LlmRuntime` project exists, it is referenced
directly. The **Generate Profile** button sends the prompt to the local runtime
and requests strict JSON containing granular settings and wavetable points.

The plugin checks these environment variables:

```text
PROJECTZ_LLM_MODEL                 optional model key
PROJECTZ_LLM_MODEL_ROOT            overrides JACKLLM_MODEL_ROOT
PROJECTZ_LLM_COMPLETE_MODEL_ROOT   overrides JACKLLM_COMPLETE_MODEL_ROOT
```

It also discovers LM Studio models. If no compatible local model is available,
the UI reports the runtime error and creates a deterministic prompt-derived
profile so the sound-design workflow remains usable.

## Edit the UI in Visual Studio

1. Open `Project Z.sln` in Visual Studio 2022 or newer.
2. Open `Project Z VST/PluginScene.xaml`.
3. Choose **Design** or **Split** in the XAML editor.

`PluginScene.xaml` is a WPF Page for the Visual Studio designer and is also
embedded as raw XAML for `SceneXamlParser`. Use controls supported by Project
Z's proxy parser, including Grid, Border, TextBlock, Button, CheckBox, Slider,
and TextBox.

## Build and install

```powershell
dotnet build ".\Project Z VST\Project Z VST.csproj" -c Release -p:Platform=x64
```

Successful builds deploy the generated bridge and adjacent managed/runtime
files to `C:\Program Files\Common Files\VST3\Project Z Granulizer Synth`. If FL
Studio has those files locked, deployment is staged and completes automatically
when FL exits. The bridge, CLR, DAW, renderer, and DirectX device must all use
the same x64 architecture.

## Main files

- `GranularEngine.cs` is the allocation-free audio processor.
- `ProfileImportService.cs` maps sound, picture, and text files into profiles.
- `LlmSoundProfileService.cs` integrates SocketJack LlmRuntime.
- `PluginScene.xaml` is the editable editor design.
- `PluginEditorScene.cs` binds Project Z controls to VST automation.
- `ProjectZDirectXHost.cs` embeds and ticks the native DirectX render HWND.
