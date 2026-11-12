# Display cadence on Windows

Set V-sync and the target frame interval before the graphics device is created.

```csharp
using Microsoft.Xna.Framework;
using ProjectZ.Windows;

// In the Game constructor, after creating its GraphicsDeviceManager:
DisplayCadence.Apply(this, graphics, verticalSync: false, targetFramesPerSecond: 120);
```

MonoGame's fixed-step loop uses the target interval for updates. Variable-step hosts can use the same interval in their own frame limiter. V-sync and the frame target are independent. GPU work and display timing can reduce the observed frame rate.

To apply a V-sync change to an existing device, call `GraphicsDeviceManager.ApplyChanges()` on the graphics thread when the host can safely reset its device state. `DisplayCadence.Apply` itself leaves the existing device alone.

The Windows DirectX 12 backend uses a DXGI flip-discard swap chain. On systems that report tearing support, it enables the swap chain flag at creation and the presentation flag for windowed frames with V-sync off. `DisplayCadence.DxgiTearingSupported` reports that capability. Variable refresh such as FreeSync also depends on a compatible monitor, driver settings, and Windows presentation path.

DirectX 11 and OpenGL retain their backend V-sync and immediate presentation modes. Layered windows presented by Desktop Window Manager, including surfaces using `UpdateLayeredWindow`, follow the desktop compositor's timing even when their offscreen renderer uses the DirectX 12 backend.

[Microsoft DXGI variable refresh documentation](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/variable-refresh-rate-displays) · [Pinned MonoGame DirectX 12 implementation](https://github.com/MonoGame/MonoGame/blob/5e9fe9b100ffa67026dff97cc8adffae1414534e/native/monogame/directx12/DeviceResources.cpp)
