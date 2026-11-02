// Separable Gaussian, paired bilinear taps: 17 texels in nine texture reads.
// Input is prefiltered at reduced resolution; the final material uses stable noise.
#if DIRECTX12
#define PS_MODEL ps_6_0
#define PS_TARGET SV_Target0
Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);
#define SAMPLE(p) SourceTexture.Sample(SourceSampler, p)
#elif OPENGL
#define SV_POSITION POSITION
#define PS_MODEL ps_3_0
#define PS_TARGET COLOR0
sampler2D SourceTexture : register(s0);
#define SAMPLE(p) tex2D(SourceTexture, p)
#else
#define PS_MODEL ps_4_0
#define PS_TARGET COLOR0
sampler2D SourceTexture : register(s0);
#define SAMPLE(p) tex2D(SourceTexture, p)
#endif
float2 BlurStep;
float2 ScreenOrigin;
float2 DesktopExtent;
float Saturation;
float NoiseOpacity;
float4 MaterialTint;
float Mode;
struct SpriteInput { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
float4 DesktopMaterial(SpriteInput input) : PS_TARGET {
    float2 uv = input.TexCoord;
    if (Mode < 0.5) {
        // A normalized sigma=3 discrete Gaussian, truncated at eight texels.
        float4 c = SAMPLE(uv) * 0.13357122;
        c += (SAMPLE(uv + BlurStep * 1.4584295) + SAMPLE(uv - BlurStep * 1.4584295)) * 0.23228859;
        c += (SAMPLE(uv + BlurStep * 3.4039848) + SAMPLE(uv - BlurStep * 3.4039848)) * 0.13533215;
        c += (SAMPLE(uv + BlurStep * 5.3518058) + SAMPLE(uv - BlurStep * 5.3518058)) * 0.05197315;
        c += (SAMPLE(uv + BlurStep * 7.3029407) + SAMPLE(uv - BlurStep * 7.3029407)) * 0.0136205;
        return float4(c.rgb, 1);
    }
    float3 color = SAMPLE(uv).rgb;
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    color = lerp(luminance.xxx, color, Saturation);
    color = lerp(color, MaterialTint.rgb, MaterialTint.a);
    float2 p = floor(uv * DesktopExtent + ScreenOrigin);
    float noise = frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715)))) - 0.5;
    return float4(saturate(color + noise * NoiseOpacity), 1) * input.Color;
}
technique DesktopBlur { pass P0 { PixelShader = compile PS_MODEL DesktopMaterial(); } }
