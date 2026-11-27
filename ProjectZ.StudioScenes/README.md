# ProjectZ.StudioScenes

Share a terrain, imported 3D model, or combined scene between Project-Z hosts. The renderer accepts explicit time in seconds so preview, seeking, and export can sample the same pose.

A **.pzscene3d** file stores edited settings in versioned JSON. It carries the terrain seed, relief, noise and water parameters, camera, model placement, animation selection, playback options, and an optional two-bone IK chain. The manifest references model files.

- Place the model beside the scene file or in a subfolder. The manifest records its relative path. Transfer the scene and its assets together.
- Project-Z Model Import reads OBJ, FBX, glTF, GLB, and Collada. An imported rig can sample its embedded clips or use a bind pose. IK applies to a directly parented three-joint chain in model-space units.
- GLB can carry geometry, animation, and textures in one file. If the model uses external dependencies, retain its sidecar layout and use models you trust. External textures follow the model importer's normal resolution rules.
- The renderer works independently of a window and graphics device. It produces premultiplied BGRA pixels over a transparent background. Enable the two-color sky gradient to fill that background. Each playback or export worker should own its renderer.
- A host can supply normalized audio levels and onset strength through the `StudioSignal` record. Matching settings, time, and signal produce the same render in either host.

```csharp
using ProjectZ.StudioScenes;

var scene = StudioSceneFile.Load(path);
var modelFile = StudioSceneFile.ResolveModelPath(path, scene);
using var renderer = new StudioSceneRender(scene, modelFile);
var bgra = renderer.CopyFrame(1280, 720, 1.25);
```

Applications building the Project-Z dependency from source can pass `StudioScenesProjectZRoot` to the Studio Scenes project. It must point to the source checkout used for the host's Project-Z runtime and Model Import assemblies.


## Terrain resolution and materials

The terrain grid controls geometric detail. It accepts 9 to 257 samples per side, with 65 as the default. The upper limit is 66,049 vertices and 131,072 land triangles. The material map has its own setting from 64 to 1024 samples per side, with 256 as the default. The animated water grid is limited to 65 samples per side.

Sand, soil, grass, rock, and snow generate from the scene seed and world coordinates. Altitude and steepness drive the blend between layers. Use material texture strength to mix the generated palette into the broad land colors. Use texture scale to change the detail size in world units.

The Coastal, Temperate, Highland, Alpine, and Arid altitude presets fill in editable height and slope thresholds. Snow can be disabled independently. A scene created before materials were added uses the default material values when opened. A scene file stores these settings without separate texture images.

## Endless worlds and camera animation

Enable Endless terrain to travel beyond the island's original bounds. The seed and world coordinates define each chunk, including its edge heights, normals, material samples, and animated water phases. Revisiting a location at the same scene time and signal reproduces the frame. The water combines seeded spatial variation with continuous time and audio inputs.

Fog blends distant ground and water into the sky or transparent background. The CPU renderer reuses chunk topology, skips water under dry ground, and caches the rendered land and depth at a fixed camera. Active camera movement and higher geometry or material resolutions require more CPU time. The surrounding host still needs its own supported graphics backend.

Choose Flythrough, Orbit, or Keyframes on the Path page. The beat grid snaps authored keys. Playback interpolates camera orientation, position, zoom, and field of view between those keys. When a host supplies track tempo or beat position, Sync to track lets the camera follow it.

## Open ocean

Choose **Open ocean** in the 3D Scene Editor or start with `StudioScene.CreateOcean()`. Both preview and export use the same four wind-driven spectral cascades, inverse FFT, choppy displacement, and Jacobian foam from a shared scene. The surface lighting uses roughness-aware Fresnel reflection, Beckmann highlights, and water scattering. The **Ocean** tab exposes wind, depth, wave height, foam, audio response, eye height, and lighting. Wave phases use absolute scene time so playback and timeline seeks agree.

The spectral and lighting methods adapt [Garrett Gunnell’s Water](https://github.com/GarrettGunnell/Water) and [Ivan Pensionerov’s FFT Ocean](https://github.com/gasgiant/FFT-Ocean). See `LICENSE.Water.txt` for the MIT notices.
