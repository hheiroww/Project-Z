# Model rigs and animation

The Windows `ProjectZ.ModelImport` co-package imports FBX, glTF/GLB, and Collada model hierarchies, skin weights, diffuse textures, and joint animation clips. OBJ remains available for static surfaces. Imported meshes expose bind-pose vertices through their existing `Vertices` property. Animated layers retain local bind vertices and per-mesh joint bindings.

`Model3DAsset.Rig` exposes parent-ordered joints and clips. Sample a clip at an explicit time to create an independent pose. Sampling interpolates translations and scales linearly and interpolates rotations with quaternion slerp. Looping is optional.

```csharp
var asset = ModelImporter3D.Load("character.fbx");
var pose = asset.Rig.Sample(seconds: 0.8, animationIndex: 0, loopAnimation: true);
pose.ApplyTwoBoneIk(rootIndex, elbowIndex, wristIndex, handGoal, elbowPole, weight: 0.85f);
var skinned = asset.Meshes[0].DeformVertices(pose);
```

Use model-space goal and pole positions with a direct root, bend, and tip chain. Partial IK weight blends with the sampled animation. Mesh deformation preserves UV coordinates and applies the skin matrices to positions and normals.

This importer supports joint translation, rotation, and scale channels. Morph-target animation, animation events, source constraints, retargeting, and importer-specific curve tangents require separate support.

Run the numerical checks with `dotnet run --project tests/ModelRig/ModelRig.vbproj -c Release`.
