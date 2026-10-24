# Project Z identity

The green Z is shared by the GitHub README, repository social preview and
the embedded NuGet package icon. The SVG files are the editable originals.

| Asset | Use |
| --- | --- |
| `project-z-icon.svg` | Square vector original. |
| `project-z-icon.png` | 128 x 128 NuGet icon, with transparent outer corners. |
| `project-z-icon-512.png` | 512 x 512 export. |
| `project-z-social.svg` | Editable social-preview layout. |
| `project-z-social.png` | 1280 x 640 GitHub social preview. |

Render the PNG exports with Node.js and sharp:

```powershell
npm install --prefix artifacts/brand-tools sharp
$env:NODE_PATH = (Resolve-Path artifacts/brand-tools/node_modules).Path
node tools/build-brand-assets.cjs
```

NuGet embeds the 128-pixel image through `PackageIcon`. GitHub's social-preview
image is uploaded separately through repository settings.
