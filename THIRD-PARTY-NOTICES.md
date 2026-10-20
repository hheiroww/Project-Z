# Third-party notices

Project-Z's native shader implementation includes adaptations from
WPFPixelShaderLibrary. Its GNU General Public License version 3 text is included
at `licenses/WPFPixelShaderLibrary/LICENSE` in the NuGet package. Corresponding
Project-Z shader source is maintained in `Resources/XamlEffects.fx` in
https://github.com/hheiroww/Project-Z.

The native MonoGame runtime and its component notices are included under
`tools/native/win-x64/LICENSE.*.txt`. These cover MonoGame, DirectX,
D3D12 Memory Allocator, SDL and stb. Runtime provenance is in the accompanying
`runtime.json` and the repository's `ThirdParty/MonoGame.Native` directory.

The runnable XAML tool under `tools/xaml-codegen` includes Microsoft Roslyn,
MSBuild Locator, Humanizer and System.Composition dependencies. Their package
identities and versions are recorded in `ProjectZ.XamlCodegen.deps.json`.
The MIT license and Microsoft's additional third-party notices are included
under `licenses/XamlCodegen`. The build tool's dependency list is
declared in `Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj` in the source
repository.

The sample Segoe UI `.xnb` files under `tools/content/Fonts/Segoe UI` contain
compiled SpriteFont bitmap content. Segoe UI is a Microsoft font; the framework
also supports using the installed Windows Segoe UI font through its vector text
renderer. No raw Microsoft font file is included in this package.
