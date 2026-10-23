# Passive DWM backdrops

`DwmBackdropHost` supplies acrylic behind its owner without activating a second
application window. Its form uses `WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`, and
`ShowWithoutActivation`, including on the first presentation.

The companion starts hidden, takes its owner's bounds before showing, and is
positioned immediately behind the owner. Hiding or minimizing the owner hides
the companion even if it has not completed its first visible synchronization.
Dispose the host when disposing the owner.

When sharp content is presented on an additional layered window, that surface
must follow the owner's topmost state and remain above the owner. Apply tint or
opacity to background pixels while preserving the content's alpha.
