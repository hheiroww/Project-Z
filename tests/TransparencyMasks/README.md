# Transparent background masks

`dotnet run --project tests/TransparencyMasks -c Release -- <result-directory>`
renders real DX12 pixels, using 0x and 16x antialiasing, and checks transparent
slider thumbs at 0%, 33%, 49% and 100% opacity and both range endpoints.
The thumb must contain one alpha layer, with the track excluded beneath it.
The check also covers overlapping cutouts, rounded gradient coordinates and
parent clipping. PNG files and `result.txt` are written to the result directory.

`RectangleElement.BackgroundCutouts` supplies rectangular background holes in
the same layout coordinates as the element's `Rectangle`. It masks the fill and
border while preserving children, pointer handling, existing clipping and brush
coordinates. A dynamic callback can follow another element as it moves:

```vb
track.BackgroundCutouts = Function() {thumb.Rectangle}
```

`Trackbar` uses this mask automatically. `TrackColor` and `ThumbColor` expose
independent colors, and `IsDragging` allows a theme to select a pressed color.
