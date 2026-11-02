# Platform integration pathways

This is the portability review for merging the Linux/macOS targets. Each row
identifies a working default or an explicit application integration contract.
Provider hooks are supported extension points; they do not bundle an untested
third-party native engine into the base UI runtime.

| Windows-specific behavior | Linux/macOS pathway | Verification / boundary |
| --- | --- | --- |
| DWM/accent glass | [Shared shader desktop blur](DESKTOP-BLUR.md), with native capture exclusion and opaque fallback. Linux offers managed X11 dependency installation. | The same Gaussian/material shader is compiled for DX12, DX11 and OpenGL. Capture permissions and Wayland session restrictions are explicit. |
| Windows chrome / hooks | MonoGame `GameWindow` resizing, SDL mouse/keyboard/text events and framework-drawn chrome. Native host decoration remains an application/OS concern. | Native package smoke tests cover SDL text, clipboard and scene rendering; no global hook is required. |
| WPF system-font lookup / custom font files | Compiled ProjectZ Sans remains the default. Register `VectorFont.Provider` (`IVectorFontProvider` / `IVectorFontFace`) for custom files, system lookup, shaping and colored glyph rendering. Prebuilt MonoGame `SpriteFont` assets also remain supported. | The provider supplies both measurement and drawing. Face ownership/disposal and family/file dispatch are checked. Advanced bidi/IME behavior still needs an application input adapter. |
| WPF spelling | Set `OfflineSpellChecker.Provider` on the checker assigned to `Textbox.SpellChecker`, or on the default checker at startup. `IsAvailable` includes that provider. | The existing debounce, underline, suggestions and app dictionary stay in use. UTF-16 ranges, cancellation, dictionary filtering and provider failures are checked. `IsSupported` continues to describe the built-in Windows engine. |
| Windows media decoding | Attach an `IMediaPlaybackController` to `MediaElement.PlaybackController`. Feed owned textures via `PresentFrame`; report lifecycle using `SignalOpened`, `SignalEnded`, `SignalFailed`. | Open/play/pause/stop/seek/volume routing, frame counts and texture ownership are checked. The application chooses and distributes its decoder/audio engine. |
| WebView2 | Keep WebView2 in the Windows target. Use a platform webview adapter, or open the same web content in the system browser on Linux/macOS. | `Html5EngineHost` is Windows-only and is not advertised as a portable embedded browser. Runtime scene XAML does not need a webview. |
| Windows crash observer / minidumps | Portable `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and application logging; use platform .NET dump tooling for native diagnostics. DesktopGL render failures also go to `System.Diagnostics.Trace`. | `ProjectZ.Diagnostics` remains a Windows sidecar. Add a `TextWriterTraceListener` for render diagnostics; the app controls destination/retention. |
| Assimp co-package's Windows target | Procedural `SurfaceMesh3D` needs no importer. Register `ModelImporter3D.Provider` (`IModelImporter3D`) for portable file loading. | OBJ/FBX input validation and async cancellation remain shared. The provider path is tested without the Windows co-package or its native libraries. |
| WPF compatibility runtime / designer | Use native scene XAML and `ProjectZ.XamlTools` with C# `ProjectZSceneXaml` items. WPF designer/imported compatibility applications retain a Windows target until adapted. | The native compiler is a `net8.0` build tool; the WPF compatibility assembly and Windows-oriented templates are not added to portable packages. |

Register font/model providers at startup before loading content. Existing font
instances retain their selected face. `IVectorFontFace.Draw` runs on the game
thread, uses logical coordinates, honors `GraphicsQuality.RenderScale`, and must
restore any GPU state it changes. Providers own their shaping/caching behavior;
`VectorFont` owns and disposes each returned face.

Spelling providers return `Task<IReadOnlyList<SpellingIssue>>`; issue ranges refer
to the supplied UTF-16 string. The framework filters words accepted by the app
dictionary and rejects out-of-range results. Application-provided engines own
their own privacy/offline policy. Plain DesktopGL textboxes skip unavailable
built-in spelling rather than retrying an unsupported engine every frame.

Media controllers are application-owned. Attach one before `Open`, and deliver
textures and lifecycle callbacks on the game thread. Stop/release the decoder
when its view is disposed; the control calls `Stop` but never takes ownership of
the controller or an externally supplied texture. Late frames do not resume a
paused controller-backed view.

The portability contract is feature-specific: an OS permission, external engine,
or alternative desktop session may be required. Unsupported features remain
discoverable and have an explicit fallback; they are not presented as WPF/Win32
feature parity.
