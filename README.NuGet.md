![Project Z](https://raw.githubusercontent.com/hheiroww/Project-Z/master/Images/branding/project-z-icon.png)

# Project Z

**Familiar XAML. Native graphics. Expressive desktop UI.**

Build desktop tools, creative applications and game interfaces with a GPU-rendered scene system, native Windows integration and a WPF-inspired programming model.

[![NuGet](https://img.shields.io/nuget/v/ProjectZ?color=21c982&logo=nuget)](https://www.nuget.org/packages/ProjectZ)
[![Build](https://github.com/hheiroww/Project-Z/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hheiroww/Project-Z/actions/workflows/dotnet.yml)

[Get started](#get-started) · [See it in action](#heirowsnap-built-with-project-z) · [Documentation](#documentation) · [Visual Studio templates](https://github.com/hheiroww/Project-Z/blob/master/ProjectTemplate/VS2026/README.md)

![Current heirowSnap gallery and Graphics settings with vibrant native DWM blur over a colorful desktop](https://raw.githubusercontent.com/hheiroww/Project-Z/master/Images/showcase/heirowsnap-glass.png)

*heirowSnap 26.10.64, built with Project Z. Native glass, sharp vector text and a shared application theme.*

## Get started

Add the framework to a Windows .NET 8 or .NET 10 project:

```powershell
dotnet add package ProjectZ --version 2026.10.25.1
```

For a ready-to-run C# or Visual Basic starter, [download the Visual Studio 2026 templates](https://github.com/hheiroww/Project-Z/releases/download/vs2026-2026.10.25.1/ProjectZ.VisualStudio2026.vsix). Install the **.NET desktop development** workload and **.NET 10 SDK**, install the VSIX, then choose **File → New → Project → Project-Z Desktop**.

The templates offer **DirectX 12**, **DirectX 11** and **OpenGL** configurations. The framework defaults to native DX12. [Template setup and renderer selection](https://github.com/hheiroww/Project-Z/blob/master/ProjectTemplate/VS2026/README.md).

## heirowSnap built with Project Z

A native file gallery and Graphics panel share the same transparent theme. Watch the gallery scroll while a colorful desktop moves behind the windows, then explore the native Graphics controls.

![heirowSnap in action: gallery scrolling, native Graphics settings and colorful DWM glass](https://raw.githubusercontent.com/hheiroww/Project-Z/master/Images/showcase/heirowsnap-in-action.gif)

Captured from the installed **26.10.64** app with an offline profile, staged sample images, **16x SSAA** and **anisotropic 16x** filtering. [Full-size gallery](https://raw.githubusercontent.com/hheiroww/Project-Z/master/Images/showcase/heirowsnap-gallery.png) · [Capture details](https://raw.githubusercontent.com/hheiroww/Project-Z/master/Images/showcase/README.md).

## What you can build

| Capability | Included |
| --- | --- |
| **Native rendering** | DX12 scenes, reusable render targets, vector curves and configurable antialiasing. |
| **XAML interfaces** | Layout panels, resources, styles, C#/VB code-behind conversion and native scene generation. |
| **Windows glass** | DWM blur, background transparency, custom window chrome and shared tool windows. |
| **Text and input** | Compiled free vector fonts, system color emoji, selection, editable text, offline spelling and keyboard navigation. |
| **Effects and motion** | GPU blur, shadows, gradients, transforms, storyboards and input triggers. |
| **Media and creative tools** | Inline audio/video, 3D surfaces, separate VST/sample projects. |

The framework package includes native XAML code generation, the internal model importer and a self-contained diagnostics observer. The heirowSnap app, VST plugin and demos are separate projects.

## Bring your XAML

Keep your markup, resources and C# or VB code together. Generate native scenes during the build, or import supported views explicitly. Existing manual ports can keep conversion disabled; original sources remain editable.

[Native scene setup and examples](https://github.com/hheiroww/Project-Z/blob/master/docs/BUILD-TIME-XAML.md) · [Build-time XAML conversion](https://github.com/hheiroww/Project-Z/blob/master/docs/BUILD-TIME-XAML.md) · [Implementation guide](https://github.com/hheiroww/Project-Z/blob/master/docs/GETTING-STARTED.md)

## Build from source

```powershell
dotnet restore "Project Z Windows.vbproj"
dotnet build "Project Z Windows.vbproj" -c Release -p:UseLocalSocketJack=false
```

The DX11 fallback is available with `-p:ProjectZGraphicsBackend=DirectX11`; use `OpenGL` for the OpenGL backend. [Examples and solution projects](https://github.com/hheiroww/Project-Z/blob/master/docs/GETTING-STARTED.md#explore-the-solution).

## Documentation

- [Feature reference](https://github.com/hheiroww/Project-Z/blob/master/docs/FEATURES.md)
- [Native scene XAML](https://github.com/hheiroww/Project-Z/blob/master/docs/BUILD-TIME-XAML.md) and [build-time conversion](https://github.com/hheiroww/Project-Z/blob/master/docs/BUILD-TIME-XAML.md)
- [Visual Studio 2026 templates](https://github.com/hheiroww/Project-Z/blob/master/ProjectTemplate/VS2026/README.md)
- [Offline spelling and custom dictionaries](https://github.com/hheiroww/Project-Z/blob/master/docs/Spellchecking.md)
- [Selectable and read-only text](https://github.com/hheiroww/Project-Z/blob/master/docs/TEXT-SELECTION.md)
- [Third-party notices](https://github.com/hheiroww/Project-Z/blob/master/THIRD-PARTY-NOTICES.md)

## Current release

**2026.10.25.1** removes the unused HTML5 scene engine and its automatically copied web assets. Updated C# and Visual Basic XAML templates keep original WPF code-behind out of the native app compiler, preventing duplicate constructors and ambiguous window types in Visual Studio. Native DX12, DX11, OpenGL and imported WebView2 controls remain supported. [Package versions](https://www.nuget.org/packages/ProjectZ#versions-body-tab).

## Support

[Support Project Z](https://github.com/hheiroww/Project-Z/blob/master/SPONSORING.md) through optional GitHub sponsorship.
