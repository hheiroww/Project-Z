# Project-Z native GPU fence fix

This is a source-built Windows x64 DX12 runtime for MonoGame 3.8.5.1, pinned
to upstream commit `5e9fe9b100ffa67026dff97cc8adffae1414534e`.
`GpuFence.patch` contains the changes; `Build.ps1` fetches the exact sources
and submodules, compiles their shaders, builds the DLL and PDB, and optionally
runs real GPU regression tests. Visual Studio C++ tools and CMake are required
to rebuild. Normal framework/app builds use the checked-in runtime payload.

```powershell
./ThirdParty/MonoGame.Native/Build.ps1 -Tests
```

Upstream turns a five-second CPU fence timeout into `DXGI_ERROR_DEVICE_HUNG`
without checking the driver's device-removal status. Its `noexcept` GPU cleanup
can then throw again while unwinding and abort with `0xC0000409`, reason 7.
The patch waits for completion while the driver reports a healthy device,
reports true device removal, checks failed queue signals, and handles the
removed-device fence sentinel. Cleanup cannot throw across the `noexcept`
boundary. Missing device-loss callbacks produce an HRESULT rather than a null
function call.

`tests/GraphicsFence/FenceWait.cpp` delays a real GPU queue for 6.5 seconds,
then verifies actual device removal and removed-device cleanup. The unpatched
queue fails the healthy-delay test with `0x887A0006`; the patched queue passes.
The test removes only its own D3D12 device and does not trigger a system GPU reset.

Project-Z owns the runtime, build integration and diagnostic observer. Native
graphics HRESULTs and first-exception dumps are captured before cleanup; GPU
delay/completion notices, adapter identity and native DLL hashes are logged.
The framework's build/publish targets apply the same payload to independent
consumers. No heirowSnap dependency is introduced.

The patch addresses false fence-hang detection and fatal cleanup; it does not
repair hardware or driver faults. A genuinely removed device remains an error.
MonoGame, SDL and stb retain their original licenses in the adjacent LICENSE files.
