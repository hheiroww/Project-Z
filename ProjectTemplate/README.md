# Project Z Application — Visual Studio Project Template

A Visual Studio project template for creating new MonoGame game applications using the **Project Z** shared framework (SceneManager, Scene, ContentContainer, etc.).

---

## 📁 Template Contents

| File | Purpose |
|---|---|
| `MyTemplate.vstemplate` | VS template manifest (defines metadata and project structure) |
| `ProjectTemplate.vbproj` | Templatized SDK-style project file |
| `Program.vb` | Application entry point |
| `GameWindow.vb` | Main game window — **edit this** to set up scenes |
| `GameWindow_Generated.vb` | Internal game loop plumbing (Draw/Update/LoadContent) |
| `Scenes\MainScene.vb` | Starter scene — add your game elements here |
| `Properties\AssemblyInfo.vb` | Assembly metadata with replaceable parameters |

### Replaceable Parameters

The template uses standard Visual Studio replaceable parameters:

- `$safeprojectname$` — Project name safe for use as a namespace
- `$projectname$` — Project display name
- `$guid1$` — A new unique GUID
- `$year$` — Current year

---

## 🚀 How to Install

### Option 1 — User Template (recommended for personal use)

1. **Zip** the contents of this `ProjectTemplate` folder (all files including subfolders, but **not** the `ProjectTemplate` folder itself — the `.vstemplate` file must be at the root of the `.zip`).

2. Copy the `.zip` file to your Visual Studio user templates directory:

   ```
   %USERPROFILE%\Documents\Visual Studio 2022\Templates\ProjectTemplates\
   ```

   > For Visual Studio 2026 Insiders, the path may be:
   > `%USERPROFILE%\Documents\Visual Studio 2026\Templates\ProjectTemplates\`

3. **Restart Visual Studio** (or it will be picked up automatically next time you open the New Project dialog).

4. Go to **File → New → Project**, search for **"Project Z Application"** and create a new project.

### Option 2 — Share with your team

Distribute the `.zip` file to teammates. Each person copies it into their own user templates directory (step 2 above).

### Option 3 — VSIX Extension (advanced)

For broader distribution (e.g., Visual Studio Marketplace), wrap the template in a VSIX project:

1. In Visual Studio: **File → New → Project → VSIX Project**.
2. Add the `.zip` template as an asset in `source.extension.vsixmanifest`.
3. Build the VSIX and distribute the `.vsix` installer.

---

## ⚙️ Prerequisites

When creating a project from this template, ensure your solution also contains or references the **Project Z** shared library (`Project Z Windows.vbproj`). The generated `.vbproj` includes:

```xml
<ProjectReference Include="..\Project Z Windows.vbproj" />
```

If the shared library is distributed as a NuGet package, replace the `ProjectReference` with a `PackageReference` in the template's `.vbproj` before zipping.

---

## 🎮 Getting Started After Creating a Project

1. **`GameWindow.vb`** — Configure window size and set up your scenes in `SetupSceneManager()`.
2. **`Scenes\MainScene.vb`** — Add `SceneElement` instances using `AddElement()` in the constructor.
3. **`Content\`** — Place your `.xnb` content files (fonts, textures) and add `<Content>` entries to the `.vbproj`.

---

## 📝 Quick-Create ZIP (PowerShell)

Run from the repository root:

```powershell
Compress-Archive -Path "ProjectTemplate\*" -DestinationPath "ProjectZ_Template.zip" -Force
```

Then copy `ProjectZ_Template.zip` to your templates directory.
