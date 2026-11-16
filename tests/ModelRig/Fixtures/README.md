# Synthetic rig test assets

These locally authored models contain a weighted triangle bound to three joints and a second triangle translated four units along X. The two-second Swing clip moves the root one unit along Y and rotates the middle joint 90 degrees around Z. The separate Grow clip doubles the tip joint's scale.

The glTF and GLB fixtures share the same geometry and animation data. The ASCII FBX was exported via Assimp with corrected `Lcl Scaling` connections, and the Collada export includes explicit animation sources for both clips. The fixtures contain no external textures or geometry.
