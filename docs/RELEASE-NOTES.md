# Project-Z 2026.11.11.1

- Images and video blend correctly with transparency and parent opacity. Image
  changes and newly presented video frames refresh on-demand scenes.
- Nested clips and the host's active scissor apply through effects and
  antialiasing resolves, including when UI is rendered at a different scale.
- Windows video displays an initial still and updates after a paused seek.
  Playback keeps the newest frame and uses a precise 30 Hz presentation
  schedule where the Windows timer is available.

All seven runtime packages ship together. The combined Visual Studio template
extension uses the matching framework version and includes the Windows Desktop,
XAML Designer, and cross-platform starters.
