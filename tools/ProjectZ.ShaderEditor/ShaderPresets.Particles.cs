namespace ProjectZ.ShaderEditor;

public static partial class ShaderPresets
{
    private static ShaderPreset[] Particles() =>
    [
        Recipe("drifting-snow", "Drifting Snow", "Particles",
            "Two depths of soft flakes drift sideways as they fall.",
            """
            float4(A.rgb + B.rgb * C.x * (
                (1.0 - smoothstep(0.08, 0.25, length(frac(float2(uv.x * 15.0 + 0.4 * sin(uv.y * 9.0 + Time * C.y), uv.y * 13.0 - Time * C.y * 1.4)) - 0.5))) *
                    step(0.62, NoiseHash(floor(float2(uv.x * 15.0 + 0.4 * sin(uv.y * 9.0 + Time * C.y), uv.y * 13.0 - Time * C.y * 1.4))))
                + 0.42 * (1.0 - smoothstep(0.04, 0.21, length(frac(uv * 29.0 - float2(Time * C.y * 0.28, Time * C.y * 0.7)) - 0.5))) *
                    step(0.65, NoiseHash(floor(uv * 29.0 - float2(Time * C.y * 0.28, Time * C.y * 0.7))))
            ), A.a)
            """, "Flake color", [0.82, 0.92, 1.0, 1], "Brightness", 0.85, "Fall speed", 1.0),

        Recipe("windblown-rain", "Windblown Rain", "Particles",
            "Thin diagonal drops streak through the frame at uneven lengths.",
            """
            float4(A.rgb + B.rgb * C.x *
                (1.0 - smoothstep(0.012, 0.12, abs(frac((uv.x - uv.y * 0.19) * 53.0) - 0.5))) *
                (1.0 - smoothstep(0.07, 0.24, abs(frac(uv.y * 11.0 - Time * C.y * 5.0 +
                    NoiseHash(float2(floor((uv.x - uv.y * 0.19) * 53.0), 12.0))) - 0.5))) *
                (0.4 + 0.6 * NoiseHash(floor(float2((uv.x - uv.y * 0.19) * 53.0, uv.y * 11.0 - Time * C.y * 5.0)))), A.a)
            """, "Rain color", [0.46, 0.73, 1.0, 1], "Streak brightness", 0.55, "Rain speed", 1.0),

        Recipe("welding-sparks", "Welding Sparks", "Particles",
            "Short rays of hot light spray from an off-center point.",
            """
            float4(A.rgb + B.rgb * C.x * pow(saturate(1.0 - PolarUV(uv - float2(-0.15, 0.10)).y), 1.5) *
                (1.0 - smoothstep(0.015, 0.09, abs(frac(PolarUV(uv - float2(-0.15, 0.10)).x * 41.0) - 0.5))) *
                (1.0 - smoothstep(0.04, 0.17, abs(frac(PolarUV(uv - float2(-0.15, 0.10)).y * 7.0 - Time * C.y * 3.5 +
                    NoiseHash(float2(floor(PolarUV(uv - float2(-0.15, 0.10)).x * 41.0), 4.0))) - 0.5))), A.a)
            """, "Spark color", [1.0, 0.64, 0.13, 1], "Spark energy", 1.3, "Travel speed", 1.0),

        Recipe("rising-embers", "Rising Embers", "Particles",
            "Glowing coals rise, sway and cool into darker red points.",
            """
            float4(A.rgb + lerp(B.rgb * float3(0.9, 0.24, 0.18), B.rgb,
                saturate(uv.y + NoiseHash(floor(float2(uv.x * 21.0 + 0.25 * sin(uv.y * 12.0 + Time * C.y * 1.7), uv.y * 15.0 + Time * C.y * 1.3))) * 0.22)) *
                C.x * (0.2 + 0.8 * uv.y) *
                pow(saturate(1.0 - 2.5 * length(frac(float2(uv.x * 21.0 + 0.25 * sin(uv.y * 12.0 + Time * C.y * 1.7),
                    uv.y * 15.0 + Time * C.y * 1.3)) - 0.5)), 2.8) *
                step(0.58, NoiseHash(floor(float2(uv.x * 21.0 + 0.25 * sin(uv.y * 12.0 + Time * C.y * 1.7), uv.y * 15.0 + Time * C.y * 1.3)))), A.a)
            """, "Ember color", [1.0, 0.35, 0.065, 1], "Glow intensity", 1.45, "Rise speed", 1.0),

        Recipe("firefly-grove", "Firefly Grove", "Particles",
            "Slow insects drift through pockets of soft green light.",
            """
            float4(A.rgb + B.rgb * C.x *
                pow(saturate(1.0 - 2.4 * length(frac(uv * float2(13.0, 9.0) +
                    float2(0.34 * sin(uv.y * 13.0 + Time * C.y), 0.3 * cos(uv.x * 12.0 + Time * C.y * 0.7))) - 0.5)), 3.0) *
                pow(0.5 + 0.5 * sin(Time * C.y * 2.4 + 25.0 * NoiseHash(floor(uv * float2(13.0, 9.0) +
                    float2(0.34 * sin(uv.y * 13.0 + Time * C.y), 0.3 * cos(uv.x * 12.0 + Time * C.y * 0.7))))), 3.0), A.a)
            """, "Firefly color", [0.72, 1.0, 0.16, 1], "Glow", 1.05, "Pulse speed", 0.75),

        Recipe("sunlit-dust", "Sunlit Dust", "Particles",
            "Slow motes appear where a broad diagonal beam crosses the picture.",
            """
            float4(A.rgb + B.rgb * C.x * exp(-9.0 * abs(uv.y - uv.x * 0.62 - 0.24)) *
                ((1.0 - smoothstep(0.03, 0.17, length(frac(uv * 36.0 + float2(Time * C.y * 0.27, -Time * C.y * 0.19)) - 0.5))) *
                    step(0.62, NoiseHash(floor(uv * 36.0 + float2(Time * C.y * 0.27, -Time * C.y * 0.19)))) +
                 0.45 * (1.0 - smoothstep(0.04, 0.22, length(frac(uv * 18.0 - float2(Time * C.y * 0.17, Time * C.y * 0.32)) - 0.5))) *
                    step(0.64, NoiseHash(floor(uv * 18.0 - float2(Time * C.y * 0.17, Time * C.y * 0.32))))), A.a)
            """, "Beam color", [1.0, 0.78, 0.43, 1], "Mote brightness", 0.5, "Drift speed", 0.7),

        Recipe("deep-starfield", "Deep Starfield", "Particles",
            "Two layers of stars drift past the image at different speeds.",
            """
            float4(A.rgb + B.rgb * C.x * (
                (1.0 - smoothstep(0.025, 0.11, length(frac(uv * float2(47.0, 31.0) - float2(Time * C.y * 0.51, 0.0)) - 0.5))) *
                    step(0.90, NoiseHash(floor(uv * float2(47.0, 31.0) - float2(Time * C.y * 0.51, 0.0)))) +
                0.6 * (1.0 - smoothstep(0.018, 0.065, length(frac(uv * float2(94.0, 65.0) - float2(Time * C.y * 0.29, 0.0)) - 0.5))) *
                    step(0.87, NoiseHash(floor(uv * float2(94.0, 65.0) - float2(Time * C.y * 0.29, 0.0))))
            ), A.a)
            """, "Starlight color", [0.74, 0.86, 1.0, 1], "Brightness", 1.25, "Parallax speed", 0.65),

        Recipe("ascending-bubbles", "Ascending Bubbles", "Particles",
            "Bubbles rise with translucent centers and brighter rims.",
            """
            float4(lerp(A.rgb, B.rgb, saturate(C.x *
                (1.0 - smoothstep(0.017, 0.082, abs(length(frac(float2(uv.x * 13.0 + 0.28 * sin(uv.y * 15.0 + Time * C.y),
                    uv.y * 8.0 + Time * C.y * 1.4)) - 0.5) - 0.27))) *
                step(0.51, NoiseHash(floor(float2(uv.x * 13.0 + 0.28 * sin(uv.y * 15.0 + Time * C.y), uv.y * 8.0 + Time * C.y * 1.4)))))), A.a)
            """, "Bubble rim", [0.48, 0.91, 1.0, 1], "Rim opacity", 0.73, "Rise speed", 0.9),

        Recipe("paper-confetti", "Paper Confetti", "Particles",
            "Colored rectangles fall, tumble and briefly turn edge-on.",
            """
            float4(lerp(A.rgb, B.rgb * (0.62 + 0.38 * cos(float3(0.0, 2.094, 4.188) +
                6.283 * NoiseHash(floor(float2(uv.x * 21.0 + 0.24 * sin(uv.y * 11.0 + Time * C.y), uv.y * 15.0 - Time * C.y * 2.2))))),
                saturate(C.x * (1.0 - smoothstep(0.12, 0.17, abs(frac(uv.y * 15.0 - Time * C.y * 2.2) - 0.5))) *
                    (1.0 - smoothstep(0.035, 0.10, abs(frac(uv.x * 21.0 + 0.24 * sin(uv.y * 11.0 + Time * C.y)) - 0.5) /
                        max(0.24, abs(sin(Time * C.y * 3.0 + floor(uv.x * 21.0)))))) *
                    step(0.46, NoiseHash(floor(float2(uv.x * 21.0 + 0.24 * sin(uv.y * 11.0 + Time * C.y), uv.y * 15.0 - Time * C.y * 2.2)))))), A.a)
            """, "Paper palette", [1.0, 0.84, 0.97, 1], "Paper opacity", 0.9, "Fall speed", 0.8),

        Recipe("radial-starburst", "Radial Starburst", "Particles",
            "Bright dashes surge outward in repeating radial bursts.",
            """
            float4(A.rgb + B.rgb * C.x * (1.0 - smoothstep(0.70, 1.45, PolarUV(uv).y)) *
                (1.0 - smoothstep(0.02, 0.13, abs(frac(PolarUV(uv).x * 57.0) - 0.5))) *
                pow(saturate(1.0 - 3.6 * abs(frac(PolarUV(uv).y * 4.0 - Time * C.y * 1.7 +
                    NoiseHash(float2(floor(PolarUV(uv).x * 57.0), 3.0)) * 0.5) - 0.5)), 1.6), A.a)
            """, "Burst color", [1.0, 0.35, 0.66, 1], "Flash energy", 1.3, "Burst speed", 1.0),

        Recipe("aurora-curtains", "Aurora Curtains", "Procedural",
            "Soft ribbons of green and violet light undulate across the picture.",
            """
            float4(A.rgb + C.x * (
                B.rgb * exp(-9.0 * abs(uv.y - 0.42 - 0.16 * sin(uv.x * 6.0 + Time * C.y) - 0.04 * sin(uv.x * 23.0 - Time * C.y * 1.5))) +
                B.brg * 0.54 * exp(-15.0 * abs(uv.y - 0.55 - 0.12 * sin(uv.x * 8.0 - Time * C.y * 0.65))) *
                    (0.5 + 0.5 * ValueNoise(float2(uv.x * 28.0, Time * C.y * 0.6)))), A.a)
            """, "Aurora color", [0.24, 0.97, 0.54, 1], "Glow", 0.68, "Wave speed", 0.75),

        Recipe("drifting-nebula", "Drifting Nebula", "Procedural",
            "Soft fractal clouds drift around small points of starlight.",
            """
            float4(lerp(A.rgb, lerp(B.rgb * 0.16, B.rgb,
                smoothstep(0.29, 0.73, 0.62 * ValueNoise(uv * 6.0 - float2(Time * C.y * 0.13, 0.0)) +
                    0.27 * ValueNoise(uv * 14.0 + float2(0.0, Time * C.y * 0.19)) +
                    0.11 * ValueNoise(uv * 35.0))) +
                step(0.989, NoiseHash(floor(uv * 170.0))) * (1.0 - smoothstep(0.03, 0.4, length(frac(uv * 170.0) - 0.5))) * 0.75,
                saturate(C.x)), A.a)
            """, "Nebula color", [0.55, 0.22, 0.92, 1], "Overlay strength", 0.78, "Drift speed", 0.65),

        Recipe("hex-tile-field", "Hex Tile Field", "Procedural",
            "Alternating honeycomb cells light up in a moving wave.",
            """
            float4(lerp(A.rgb, B.rgb * (0.38 + 0.62 * sin(uv.x * 8.0 + uv.y * 6.0 - Time * C.y) * sin(uv.x * 8.0 + uv.y * 6.0 - Time * C.y)),
                saturate(C.x * (1.0 - smoothstep(0.025, 0.10, abs(
                    min(length(frac(uv * float2(18.0, 10.3923)) - 0.5),
                        length(frac(uv * float2(18.0, 10.3923) + 0.5) - 0.5)) - 0.33))))), A.a)
            """, "Tile light", [0.38, 0.83, 0.97, 1], "Line opacity", 0.8, "Pulse speed", 1.1),

        Recipe("flowing-plasma", "Flowing Plasma", "Procedural",
            "Interference between three moving waves creates a colored plasma field.",
            """
            float4(lerp(A.rgb, saturate(B.rgb * (0.68 + 0.32 * cos(float3(0.0, 2.094, 4.188) +
                sin(uv.x * 11.0 + Time * C.y) * 1.9 + sin(uv.y * 13.0 - Time * C.y * 0.83) * 1.6 +
                sin(length(uv - float2(0.45, 0.38)) * 21.0 + Time * C.y * 1.3) * 1.8))), saturate(C.x)), A.a)
            """, "Plasma color", [1.0, 0.56, 0.96, 1], "Overlay strength", 0.8, "Flow speed", 0.85),

        Recipe("neon-horizon-grid", "Neon Horizon Grid", "Procedural",
            "A perspective grid recedes to a horizon while its lines move toward the viewer.",
            """
            float4(A.rgb + B.rgb * C.x * smoothstep(0.0, 0.28, uv.y - 0.38) *
                (1.0 - smoothstep(0.02, 0.10, min(
                    abs(frac((uv.x - 0.5) * 10.0 / max(0.03, uv.y - 0.36)) - 0.5),
                    abs(frac(2.8 / max(0.03, uv.y - 0.36) + Time * C.y) - 0.5)))) +
                B.rgb * C.x * 0.33 * exp(-55.0 * abs(uv.y - 0.38)), A.a)
            """, "Grid color", [0.78, 0.20, 1.0, 1], "Neon brightness", 0.9, "Scroll speed", 0.9),

        Recipe("silk-waves", "Silk Waves", "Procedural",
            "Wide bands roll across the image with a satin highlight.",
            """
            float4(lerp(A.rgb, B.rgb * (0.18 + 0.68 *
                pow(0.5 + 0.5 * sin(uv.y * 24.0 + 3.0 * sin(uv.x * 7.0 + Time * C.y) +
                    1.2 * cos(uv.x * 14.0 - Time * C.y * 0.8)), 2.6)) +
                0.18 * pow(0.5 + 0.5 * sin(uv.y * 24.0 + 3.0 * sin(uv.x * 7.0 + Time * C.y) +
                    1.2 * cos(uv.x * 14.0 - Time * C.y * 0.8)), 12.0), saturate(C.x)), A.a)
            """, "Fabric color", [0.28, 0.73, 0.98, 1], "Overlay strength", 0.72, "Wave speed", 0.6),

        Recipe("voronoi-mosaic", "Voronoi Mosaic", "Procedural",
            "Stained-glass cells cast a shifting pattern of colored highlights.",
            """
            float4(lerp(A.rgb, B.rgb * (0.22 + 0.72 * smoothstep(0.08, 0.74, VoronoiFactor(
                uv * 11.0 + 0.18 * float2(sin(Time * C.y + uv.y * 8.0), cos(Time * C.y * 0.81 + uv.x * 9.0))))),
                saturate(C.x)), A.a)
            """, "Glass color", [0.17, 0.87, 0.77, 1], "Overlay strength", 0.76, "Drift speed", 0.75),

        Recipe("moire-weave", "Moiré Weave", "Procedural",
            "Rotating fine lines overlap to form broad interference bands.",
            """
            float4(lerp(A.rgb, B.rgb * (0.18 + 0.8 * pow(0.5 + 0.5 *
                sin(uv.x * 270.0 + uv.y * 44.0) *
                sin(TransformUV(uv, float2(1.0, 1.0), float2(0.0, 0.0), 0.036 * sin(Time * C.y)).x * 265.0 + uv.y * 51.0), 1.5)),
                saturate(C.x)), A.a)
            """, "Weave color", [0.94, 0.49, 0.23, 1], "Overlay strength", 0.68, "Rotation speed", 0.62),

        Recipe("moving-contours", "Moving Contours", "Procedural",
            "Contour lines reveal a drifting field like an animated elevation map.",
            """
            float4(lerp(A.rgb, B.rgb, saturate(C.x * (
                1.0 - smoothstep(0.035, 0.12, abs(frac((ValueNoise(uv * 4.2 + float2(Time * C.y * 0.15, 0.0)) +
                    ValueNoise(uv * 9.0 - float2(0.0, Time * C.y * 0.12)) * 0.33) * 15.0) - 0.5))))), A.a)
            """, "Contour color", [0.60, 0.92, 0.32, 1], "Line opacity", 0.9, "Drift speed", 0.72),

        Recipe("liquid-marble", "Liquid Marble", "Procedural",
            "Marble veins swirl around a slowly shifting center.",
            """
            float4(lerp(A.rgb, lerp(B.rgb * 0.19, B.rgb, smoothstep(0.23, 0.91, 0.5 + 0.5 *
                sin(uv.x * 18.0 + uv.y * 8.0 + 5.0 * ValueNoise(uv * 4.3 + float2(Time * C.y * 0.14, 0.0)) +
                    1.6 * ValueNoise(uv * 18.0 - float2(0.0, Time * C.y * 0.17))))), saturate(C.x)), A.a)
            """, "Marble color", [0.95, 0.71, 0.42, 1], "Overlay strength", 0.77, "Flow speed", 0.7),
    ];
}
