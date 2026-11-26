# Project-Z 2026.11.26.1

- Windows apps that use ProjectZ.Diagnostics share one observer within the same user session and elevation level. The observer closes after the last registered app exits and restarts on demand.
- The shared observer preserves separate crash logs and dumps for each application. Update the ProjectZ runtime and optional ProjectZ.Diagnostics sidecar together.

This release retains the desktop blur, rendering, physics, animation import, and cross-platform improvements shipped in 2026.11.20.1. The runtime packages and Visual Studio template extension use the same framework version.
