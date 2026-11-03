# Your Project-Z desktop application

Install the .NET 8 SDK or later, then run `dotnet run` from this directory.
The first build restores the exact Project-Z 2026.11.2.2 package from NuGet.org.
Edit `MainScene` to change the UI. This project works without a framework checkout.

The build selects one runtime for the current host: `ProjectZ` on Windows x64
(DX12), `ProjectZ.Linux` on Linux x64, or `ProjectZ.macOS` on Intel/Apple Silicon
Macs (OpenGL). An explicit publish RID selects its corresponding runtime instead:

```sh
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
```

For a build without a RID, `dotnet build -p:ProjectZPlatform=Linux` (or `macOS`
or `Windows`) selects a platform. A RID and a conflicting platform override fail
with a diagnostic. Build and publish output lives under `bin` and `obj`.

Linux needs a graphical X11/Wayland session, SDL2/OpenAL dependencies and an
OpenGL driver; macOS needs a desktop session. Desktop blur on macOS needs Screen
Recording permission. X11 blur requires XComposite/XRender; Wayland uses the
documented opaque fallback. macOS app bundles, signing and notarization are
separate deployment steps. Windows integrations and WPF conversion require a
Windows target. See the [platform guide](https://github.com/hheiroww/Project-Z/blob/master/docs/DESKTOPGL.md).

The C# scene-XAML variant compiles `MainScene.xaml` into the native partial scene
on each build through `ProjectZ.XamlTools`, a build-only dependency. Edit its
`Heading` text and rebuild. It does not need WPF or a Visual Studio designer.
The C# and Visual Basic desktop variants construct their UI directly in code.

Fonts and license notices flow from the selected runtime package. Optional
model import, browser integration and native crash diagnostics remain opt-in;
see [optional packages](https://github.com/hheiroww/Project-Z/blob/master/docs/MODULAR-PACKAGES.md).


Physics is opt-in through properties such as `RigidBody`, `CollisionEnabled`, `FreeMove`, `Weight`, `Bounce` and `FrictionType`. It drives the existing scene/animation values. The matching runtime also includes 3D bodies and GPU-batched sprite particles. See the [physics guide](https://github.com/hheiroww/Project-Z/blob/master/docs/PHYSICS.md) for examples and solver limits.
