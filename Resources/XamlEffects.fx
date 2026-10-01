#if DIRECTX12
#define PS_MODEL ps_6_0
#define PS_TARGET SV_Target0
Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);
Texture2D OriginalTexture : register(t1);
SamplerState OriginalSampler : register(s1);
#define SAMPLE(p) SourceTexture.Sample(SourceSampler, p)
#define ORIGINAL(p) OriginalTexture.Sample(OriginalSampler, p)
#else
#define PS_MODEL ps_4_0
#define PS_TARGET COLOR0
sampler2D SourceTexture : register(s0);
texture OriginalTexture;
sampler2D OriginalSampler = sampler_state { Texture = <OriginalTexture>; };
#define SAMPLE(p) tex2D(SourceTexture, p)
#define ORIGINAL(p) tex2D(OriginalSampler, p)
#endif
float2 TexelSize;
float2 BlurStep;
float2 ShadowOffset;
float4 ShadowColor;
float Amount;
float Phase;
float Angle;
float Mode;
struct SpriteInput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
float4 SoftSample(float2 uv) {
    // Separable nine-tap Gaussian. Radius is expressed in arranged device pixels.
    float4 c = SAMPLE(uv) * 0.227027;
    c += (SAMPLE(uv + BlurStep) + SAMPLE(uv - BlurStep)) * 0.194595;
    c += (SAMPLE(uv + BlurStep * 2) + SAMPLE(uv - BlurStep * 2)) * 0.121622;
    c += (SAMPLE(uv + BlurStep * 3) + SAMPLE(uv - BlurStep * 3)) * 0.054054;
    c += (SAMPLE(uv + BlurStep * 4) + SAMPLE(uv - BlurStep * 4)) * 0.016216;
    return c;
}
float4 PortedEffect(SpriteInput input) : PS_TARGET {
    float2 uv = input.TexCoord;
    if (Mode > 6.5) {
        // Exact box resolve of the 2/4/8/16 subpixel grid, including alpha.
        float4 total = 0;
        [unroll] for (int y = 0; y < 4; ++y)
            [unroll] for (int x = 0; x < 4; ++x)
                if (x < BlurStep.x && y < BlurStep.y)
                    total += SAMPLE(uv + (float2(x, y) + 0.5 - BlurStep * 0.5) * TexelSize);
        return total / (BlurStep.x * BlurStep.y) * input.Color;
    }
    if (Mode > 5.5) {
        float a = saturate(Amount) / 8;
        float f = saturate(Angle) * 6.28318530;
        float4 c = SAMPLE(uv);
        float4 r = SAMPLE(uv + float2(sin(-f), cos(-f)) * a);
        float4 b = SAMPLE(uv + float2(sin(3.14159265 - f), cos(3.14159265 - f)) * a);
        return float4(r.r, c.g, b.b, 1) * input.Color;
    }
    if (Mode > 4.5) {
        float4 c = SAMPLE(uv);
        return lerp(c, float4(c.a - c.rgb, c.a), saturate(Amount)) * input.Color;
    }
    if (Mode > 3.5) {
        // Port of WPFPixelShaderLibrary 1.0.2 SpasticChromaticAbberation.fx.
        // Original: Unknown6656, GPL-3.0. See ThirdParty/WPFPixelShaderLibrary.
        // Keep the original signed remainder, tangent displacement, scan lines,
        // color treatment and full 0..1 Amount, not a uniform RGB translation.
        float phi = fmod(abs(Phase + uv.y), 1.0) * 20 * 3.14159265;
        float f1 = sin(phi) + 0.2 * cos(16 * phi) * sin(2 * phi) + 0.5 * cos(4 * phi);
        float am = saturate(Amount);
        float fam = am > 0.7 ? 0.7 - am : am;
        bool inv1 = fmod(uv.y, (1.1 - fam) / 20) < 0.01;
        bool inv2 = fmod(uv.y, 0.05) < 0.025;
        bool inv3 = fmod(uv.y + f1, 0.09) < 0.002;
        if (inv1) f1 = -(1 + f1);
        f1 /= 15;
        uv.x = uv.x * (1 - am / 10) + uv.x * f1 * am;
        if (inv1) uv.x = saturate(uv.x - am * f1);
        float4 color = SAMPLE(uv);
        if (inv2) {
            float4 c = SAMPLE(uv);
            float4 r = SAMPLE(float2(tan(uv.x - 0.5) + 0.5125, uv.y));
            float4 b = SAMPLE(float2(tan(uv.x - 0.5) + 0.4875, uv.y));
            color = float4(r.r, c.g, b.b, 1);
        }
        if (inv3) color = SAMPLE(float2(fmod(uv.x * uv.x + uv.y + f1, 1), uv.y));
        float r2 = dot(uv - 0.5, uv - 0.5);
        float f = 1 + r2 * (0.03 * sqrt(r2));
        float4 clr = SAMPLE(uv);
        float4 clrd = SAMPLE(f * (uv - 0.5) + 0.5);
        if (!inv2) color = float4(clrd.r, clr.g, clr.b, 1);
        float3 tint = normalize(float4(200, 244, 244, 255)).rgb;
        float3 scnColor = tint * (color.rgb / max(color.a, 0.000001));
        float gray = dot(float3(0.3, 0.59, 0.11), scnColor);
        float3 muted = lerp(scnColor, gray.xxx, 0.15);
        float3 middle = tint * gray;
        scnColor = lerp(muted * 2, middle, 0.4);
        return (float4(scnColor * color.a, color.a) * am / 2 + clr * (1 - am / 2)) * input.Color;
    }
    if (Mode < 1.5) return SoftSample(uv) * input.Color;
    if (Mode < 2.5) {
        float4 original = ORIGINAL(uv);
        float alpha = SoftSample(uv - ShadowOffset).a * ShadowColor.a;
        float4 shadow = float4(ShadowColor.rgb * alpha, alpha);
        return (original + shadow * (1 - original.a)) * input.Color;
    }
    // Native approximation of the WPF chromatic-aberration family.
    float2 offset = float2(Amount, 0);
    float4 r = SAMPLE(uv + offset), g = SAMPLE(uv), b = SAMPLE(uv - offset);
    return float4(r.r, g.g, b.b, max(r.a, max(g.a, b.a))) * input.Color;
}
technique XamlEffect { pass P0 { PixelShader = compile PS_MODEL PortedEffect(); } }
