//-----------------------------------------------------------------------------
// FXAA (Fast Approximate Anti-Aliasing) Shader
// For KNI/MonoGame
//-----------------------------------------------------------------------------

#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#endif

// Parameters
float EdgeThreshold = 0.125;
float SubPixelAliasingRemoval = 0.75;

sampler2D TextureSampler : register(s0);
float2 InverseViewportSize;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

float4 FXAAPixelShader(VertexShaderOutput input) : COLOR0
{
    float2 texCoord = input.TexCoord;
    
    // Sample the center pixel and its neighbors
    float3 rgbNW = tex2D(TextureSampler, texCoord + float2(-1.0, -1.0) * InverseViewportSize).rgb;
    float3 rgbNE = tex2D(TextureSampler, texCoord + float2(1.0, -1.0) * InverseViewportSize).rgb;
    float3 rgbSW = tex2D(TextureSampler, texCoord + float2(-1.0, 1.0) * InverseViewportSize).rgb;
    float3 rgbSE = tex2D(TextureSampler, texCoord + float2(1.0, 1.0) * InverseViewportSize).rgb;
    float3 rgbM = tex2D(TextureSampler, texCoord).rgb;
    
    // Convert to luminance
    float3 luma = float3(0.299, 0.587, 0.114);
    float lumaNW = dot(rgbNW, luma);
    float lumaNE = dot(rgbNE, luma);
    float lumaSW = dot(rgbSW, luma);
    float lumaSE = dot(rgbSE, luma);
    float lumaM = dot(rgbM, luma);
    
    // Find min and max luma
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    
    // Calculate contrast
    float lumaRange = lumaMax - lumaMin;
    
    // Early exit if contrast is low
    if (lumaRange < max(0.0312, lumaMax * EdgeThreshold))
    {
        return float4(rgbM, 1.0);
    }
    
    // Calculate blend direction
    float2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y = ((lumaNW + lumaSW) - (lumaNE + lumaSE));
    
    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * SubPixelAliasingRemoval), 0.0078125);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    
    dir = min(float2(8.0, 8.0), max(float2(-8.0, -8.0), dir * rcpDirMin)) * InverseViewportSize;
    
    // Sample along the direction
    float3 rgbA = 0.5 * (
        tex2D(TextureSampler, texCoord + dir * (1.0 / 3.0 - 0.5)).rgb +
        tex2D(TextureSampler, texCoord + dir * (2.0 / 3.0 - 0.5)).rgb);
        
    float3 rgbB = rgbA * 0.5 + 0.25 * (
        tex2D(TextureSampler, texCoord + dir * -0.5).rgb +
        tex2D(TextureSampler, texCoord + dir * 0.5).rgb);
    
    float lumaB = dot(rgbB, luma);
    
    // Choose result based on luma range
    if ((lumaB < lumaMin) || (lumaB > lumaMax))
    {
        return float4(rgbA, 1.0);
    }
    else
    {
        return float4(rgbB, 1.0);
    }
}

// Pass-through technique (no FXAA, just render normally)
float4 PassThroughPixelShader(VertexShaderOutput input) : COLOR0
{
    return tex2D(TextureSampler, input.TexCoord) * input.Color;
}

technique FXAA
{
    pass Pass0
    {
        PixelShader = compile PS_SHADERMODEL FXAAPixelShader();
    }
}

technique PassThrough
{
    pass Pass0
    {
        PixelShader = compile PS_SHADERMODEL PassThroughPixelShader();
    }
}
