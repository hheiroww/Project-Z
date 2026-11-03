# Physics-driven parameters

Physics is opt-in. It changes the values used by the existing scene and animation systems; it does not replace timelines or introduce a second animation renderer. A normal control behaves as before until physics or `FreeMove` is enabled.

## 2D scene elements

```vb
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Physics
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Primitives

Dim container As New RectangleElement(scene) With {
    .Position = New Vector2(20, 20), .Size = New Vector2(500, 300),
    .CollisionEnabled = True
}
Dim item As New RectangleElement(scene) With {
    .Position = New Vector2(80, 50), .Size = New Vector2(40, 40),
    .RigidBody = True, .FreeMove = True, .CollisionEnabled = True,
    .Mass = 3, .Gravity = New Vector2(0, 980),
    .Bounce = 0.35, .FrictionType = FrictionType.Sand
}
container.Children.Add(item)
item.ApplyPhysicsImpulse(New Vector2(300, -150))
```

`RigidBody` enables dynamic integration. `RidgidBody` is an alias for the requested spelling. `Mass` and `Weight` describe the same positive mass: the same impulse produces less velocity on a heavier body. Gravity takes effect only for dynamic bodies. A collision-enabled element without `RigidBody` is a static obstacle.

`FreeMove` allows pointer dragging without requiring gravity. During a drag the pointer controls the body; release transfers a bounded sampled velocity to dynamic bodies. Input cancellation releases the drag without a throw. Mouse and touch use the scene pointer route. One primary touch contact owns a gesture; hosts can forward contacts with `SceneManager.InjectTouchDown`, `InjectTouchMove`, `InjectTouchUp`, and `CancelTouchInput`. Native touch polling and host injection are supported; physical touchscreen acceptance remains separate from the injected-contact regression checks.

By default physics drives `RenderTranslation`, which is already used by rendering, hit testing, and animation property adapters. This preserves layout anchors. `PhysicsMotion = PhysicsMotion.Position` instead changes layout position. Avoid simultaneously writing a physics-controlled component from a timeline: the physics update is the final writer for that frame. Separate components can still animate together, such as physics translation with a timeline opacity change.

The parent rectangle acts as a container **only when both parent and child have `CollisionEnabled = True`**. Siblings in the same parent collide when both enable collision. Roots share the scene's root collision group. Different parents and 2D/3D bodies use independent collision groups.

Collision shapes are axis-aligned boxes. Element and ancestor scale contribute to the 2D box; rotation and shear do not. Transparent pixels, concave outlines, and detailed 3D meshes do not change the collider into an exact shape. When a pair begins overlapped, the solver selects the shortest separating axis that fits within the parent and projects bodies out in inverse proportion to mass. A static body stays fixed. Separation does not add velocity. A child larger than its container has no valid contained placement; the solver uses a deterministic bounded placement and stops motion on the impossible axis.

## Bounce and friction

`Bounce` is the restitution coefficient, clamped to 0–1. Low-speed impacts settle instead of bouncing forever. `Friction` is a nonnegative coefficient, clamped to 0–4; setting it selects `FrictionType.Custom`.

| Friction type | Coefficient |
| --- | ---: |
| Ice | 0.02 |
| Water | 0.10 |
| Snow | 0.30 |
| Rock | 0.65 |
| Sand | 0.80 |
| Sandpaper | 1.20 |

These names are convenient motion presets, not fluid, snow, or granular material simulations.

`FrictionType.Geometry` derives a coefficient from an ordered geometry contour's compactness. Long or jagged contours have more friction than compact contours. Polygon elements supply their contour automatically; custom bodies can set `PhysicsBody.GeometryVertices` to an ordered local contour. 3D geometry uses its projection onto the contact's dominant plane. Without a usable contour, the body derives compactness from its actual box face; a degenerate face falls back to the explicit coefficient. This changes friction only; the collision shape remains an axis-aligned box. `PhysicsBody.GetFriction(normal)` returns the coefficient used for a contact.

## Existing animation parameters

`PhysicsScalar` drives any existing scalar value through a getter and setter:

```vb
Dim progress As Double = 0
Dim driver As New PhysicsScalar(Function() progress,
                               Sub(value) progress = value) With {
    .Mass = 4, .Friction = 0, .Damping = 0
}
driver.AddImpulse(80)
' Advance in the same update loop as the owning animation:
driver.Advance(gameTime.ElapsedGameTime.TotalSeconds)
```

The adapter can target an animation's progress, speed, scale, rotation, or another numeric parameter; its setter defines the actual destination. Use finite bounds for parameters with a valid range. Mass affects applied impulses and forces rather than multiplying the rendered value.

For an existing `ElementProperty`, call `item.DriveWithPhysics(property)`. The returned `PhysicsScalar` is registered with `item.PhysicsDrivers` and advanced automatically by the scene. Set `Stiffness > 0` and `Target` for a damped spring, or leave `Stiffness = 0` to coast. `Damping` defaults to 12; set it to zero for undamped motion. `Minimum`, `Maximum`, and `Bounce` constrain scalar parameters such as opacity or progress.

Kinetic `ScrollViewer` movement uses the same element `Mass`/`Weight`, `FrictionType`/`Friction`, and `Bounce`. Increasing mass reduces the velocity produced by a wheel impulse and slows frictional deceleration; a heavier scroll surface feels harder to start and stop. Existing wheel settings still set impulse strength, deceleration, and maximum speed.

## 3D and particles

`Surface3DElement` uses `RigidBody`, `CollisionEnabled`, `Mass`, `Bounce`, and friction in the same way. Its dynamic position is `Position3D`; configure `Velocity3D`, `Gravity3D`, and the collider's `PhysicsSize3D`. The default 3D gravity is `(0, -9.81, 0)` in model units, while 2D gravity is `(0, 980)` in scene pixels. `FreeMove` maps a pointer drag into the camera plane to move the model inside its viewport. The surface remains a 2D scene element for layout and compositing.

`ParticleEmitterElement` can render 2D particles beside normal controls or camera-facing 3D billboards. Its `Simulation` owns a bounded particle pool. For example:

```vb
Imports ProjectZ.Shared.Drawing.Particles
Imports ProjectZ.Shared.Drawing.UI.Advanced

Dim emitter As New ParticleEmitterElement(scene, capacity:=4096, seed:=1)
emitter.Simulation.EmissionRate = 60
emitter.Simulation.ParticleLifetime = 2
emitter.Simulation.ParticleSize = New Vector2(12)
emitter.Simulation.InitialVelocity = New Vector3(0, -50, 0)
emitter.Simulation.GravityEnabled = True
emitter.SpriteAnimation = ParticleSpriteAnimation.LoadSpriteSheet(
    graphicsDevice, "spark.png", 32, 32, secondsPerFrame:=0.08)
' For a 3D emitter, set Is3D = True, Position3D, and Camera.
```

The loader takes the host's `GraphicsDevice`; create and load textures on its graphics thread. Load a single PNG with `ParticleSpriteAnimation.LoadPng`, an animated GIF with `LoadGif`, or a uniform PNG atlas with `LoadSpriteSheet`. Custom managed definitions use `AddFrame(texture, sourceRectangle, seconds)` and may mix textures or frame durations. Shared textures remain caller-owned unless `ownsTexture:=True`; the caller owns and disposes the reusable animation. GIFs decode to a single atlas with frame delay, palette, interlace, and disposal handling. GIF loading is bounded to 256 frames, 16 million decoded pixels, and a 4096×4096 atlas.

Particle `PhysicsEnabled`, `GravityEnabled`, `Gravity`, and `LinearDrag` control particle movement. Particles do not enter scene rigid-body collisions. Capacity is 1–16,000 per emitter; invalid state is sanitized and a long frame advances at most 250 ms. For a single atlas, billboards share one texture batch per effect pass. Managed frames with different textures create batches while retaining depth order. `LastDrawCallCount` exposes the actual batch count.

## Stability and acceleration

Collision integration uses bounded fixed time steps, speed limits, swept contacts, iterative contact solving, and finite-value sanitation. A swept impact conservatively stops remaining travel for that fixed step; outgoing velocity is retained for the next step. This avoids crossing a second wall after a fast reflection at the cost of up to one fixed step of travel per impact. Pointer movement is swept too. Long stalls have bounded catch-up work. No solver can satisfy an impossible arrangement such as several immovable overlapping bodies or more geometry than a container can hold; those cases remain bounded rather than receiving escalating separation velocities.

The contact solver and pointer decisions run on the CPU. Scene transforms continue through the existing GPU renderer. Particle rendering uploads reusable billboard buffers and batches particles by texture through MonoGame's GPU pipeline. There is no GPU compute collision solver, mesh collision detection, or claim that all simulation executes on the GPU. This keeps the same physics behavior available with DirectX 12, DirectX 11, and DesktopGL.

## Regression checks

Run the numerical suite without a window or graphics device:

```powershell
dotnet run --project tests/Physics/Physics.vbproj -c Release --artifacts-path <owned-staging-directory>/physics-tests
```

It covers mass, gravity gating, collision opt-in, nearest-exit overlap recovery, restitution, friction, frame-rate consistency, fast wall impacts, independent spaces, resting contact and stacks, crowded collisions, invalid values, impossible containment, and scalar parameter control. Graphics/backend validation and native touch acceptance are separate checks.

The particle harness is `tests/Particles/Particles.vbproj`. The native XAML fixture verifies compilation of physics flags, the `RidgidBody` alias, material/motion enum literals, double coefficients, and 2D/3D vectors. The `Physics and particle regressions` workflow runs these headless checks on Windows, Linux, and macOS; it does not replace GPU or physical touchscreen acceptance.
