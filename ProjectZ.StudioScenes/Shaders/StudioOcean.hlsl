// Spectrum and water optics adapted from Garrett Gunnell's Water and Ivan
// Pensionerov's FFT Ocean. See LICENSE.Water.txt for both MIT notices.
// Fields are evaluated in world metres, independently of the viewport.

static const float Pi = 3.14159265359;
static const float Tau = 6.28318530718;
static const float Gravity = 9.81;
static const float Period = 200.0;
static const float CascadeLength[4] = { 192.0, 63.0, 19.0, 5.5 };

cbuffer Frame : register(b0)
{
    uint TargetWidth, TargetHeight, WaveSize, WaveLog2;
    uint OceanSeed, HighQuality, SkyEnabled, Padding;
    float4 Surface;           // phase, displacement scale, chop scale, foam persistence
    float4 Wind;              // speed, direction radians, fetch, depth
    float4 Material;          // swell balance, foam amount, roughness, locked frame time
    float4 Eye;               // world eye, vertical pixel angle
    float4 Forward;
    float4 RightExtent;
    float4 UpExtent;
    float4 Sun;
    float4 SkyTop;
    float4 SkyHorizon;
    float4 Atmosphere;        // cloud coverage, haze, sun strength
};

Texture2DArray<float4> ReadA : register(t0);
Texture2DArray<float4> ReadB : register(t1);
Texture2DArray<float4> FoamHistory : register(t2);
RWTexture2DArray<float4> WriteA : register(u0);
RWTexture2DArray<float4> WriteB : register(u1);
RWTexture2D<unorm float4> Output : register(u2);
SamplerState FieldSampler : register(s0);

uint Hash(uint x)
{
    x = (x ^ (x >> 16)) * 0x7feb352du;
    x = (x ^ (x >> 15)) * 0x846ca68bu;
    return x ^ (x >> 16);
}

float2 Gaussian(uint cascade, int2 index)
{
    uint key = OceanSeed * 0x9e3779b9u + cascade * 0xc2b2ae35u
        + asuint(index.x) * 0x27d4eb2du + asuint(index.y) * 0x165667b1u;
    float u = ((Hash(key) >> 8) + 1.0) / 16777217.0;
    float v = (Hash(key ^ 0x7f4a7c15u) >> 8) / 16777216.0;
    float s, c;
    sincos(Tau * v, s, c);
    return sqrt(-2.0 * log(u)) * float2(c, s);
}

float FrequencySpectrum(float omega, float peak, float alpha, float gamma)
{
    float ratio = peak / omega;
    float depthFrequency = omega * sqrt(Wind.w / Gravity);
    float tma = depthFrequency <= 1.0 ? 0.5 * depthFrequency * depthFrequency
        : 1.0 - 0.5 * pow(max(0.0, 2.0 - depthFrequency), 2.0);
    float sigma = omega <= peak ? 0.07 : 0.09;
    float f = (omega - peak) / (peak * sigma);
    return tma * alpha * Gravity * Gravity / pow(omega, 5.0)
        * exp(-1.25 * ratio * ratio * ratio * ratio) * pow(gamma, exp(-0.5 * f * f));
}

float Directional(float angle, float windAngle, float ratio)
{
    float spread = ratio > 1.0 ? 9.77 * pow(ratio, -2.5) : 6.97 * pow(ratio, 5.0);
    spread += 16.0 * tanh(min(ratio, 20.0)) * Material.x * Material.x;
    float weight = spread < 5.0
        ? (((-0.000564 * spread + 0.00776) * spread - 0.044) * spread + 0.192) * spread + 0.163
        : (((-4.80e-08 * spread + 1.07e-05) * spread - 9.53e-04) * spread + 0.059) * spread + 0.393;
    float direction = angle - windAngle;
    direction -= round(direction / Tau) * Tau;
    float c = cos(direction);
    return 0.14 * (2.0 / Pi) * c * c
        + 0.86 * weight * pow(abs(cos(0.5 * direction)), 2.0 * spread);
}

int2 SignedWave(uint2 p)
{
    int halfSize = (int)WaveSize / 2;
    int2 q = int2(p);
    return int2(q.x < halfSize ? q.x : q.x - (int)WaveSize,
        q.y < halfSize ? q.y : q.y - (int)WaveSize);
}

[numthreads(8, 8, 1)]
void InitializeCS(uint3 id : SV_DispatchThreadID)
{
    int2 index = SignedWave(id.xy);
    if ((id.x == 0u && id.y == 0u) || id.x == WaveSize / 2u || id.y == WaveSize / 2u)
    {
        WriteA[id] = 0.0;
        return;
    }
    float dk = Tau / CascadeLength[id.z];
    float2 k = index * dk;
    float kLength = length(k);
    float band = 1.0;
    if (id.z > 0)
    {
        float lower = 0.70 * 64.0 * Pi / CascadeLength[id.z - 1u];
        band *= smoothstep(0.78 * lower, 1.22 * lower, kLength);
    }
    if (id.z < 3)
    {
        float upper = 0.70 * 64.0 * Pi / CascadeLength[id.z];
        band *= 1.0 - smoothstep(0.78 * upper, 1.22 * upper, kLength);
    }
    float kh = min(kLength * Wind.w, 20.0);
    float t = tanh(kh);
    float omega = sqrt(Gravity * kLength * t);
    float propagation = Gravity * (t + kh * (1.0 - t * t)) / (2.0 * omega);
    float alpha = 0.076 * pow(Gravity * Wind.z / (Wind.x * Wind.x), -0.22);
    float peak = 22.0 * pow(Wind.x * Wind.z / (Gravity * Gravity), -0.33);
    float angle = atan2(k.y, k.x);
    float primary = FrequencySpectrum(omega, peak, alpha, 3.3) * Directional(angle, Wind.y, omega / peak);
    float swellPeak = peak * 0.77;
    float swell = FrequencySpectrum(omega, swellPeak, alpha * 0.70, 5.0)
        * Directional(angle, Wind.y - 0.85, omega / swellPeak);
    float energy = lerp(primary, swell, Material.x * 0.34) * band * exp(-pow(0.024 * kLength, 2.0));
    float amplitude = sqrt(max(0.0, 2.0 * energy * propagation / kLength * dk * dk));
    float repeatOmega = floor(omega * Period / Tau) * Tau / Period;
    WriteA[id] = float4(Gaussian(id.z, index) * amplitude, repeatOmega, 1.0 / kLength);
}

[numthreads(8, 8, 1)]
void EvolveCS(uint3 id : SV_DispatchThreadID)
{
    float4 mode = ReadA.Load(int4(id, 0));
    uint2 mirrored = (WaveSize - id.xy) & (WaveSize - 1u);
    float2 partner = ReadA.Load(int4(mirrored, id.z, 0)).xy;
    float s, c;
    sincos(mode.z * Surface.x, s, c);
    float2 height = float2((mode.x + partner.x) * c - (mode.y + partner.y) * s,
        (mode.x - partner.x) * s + (mode.y - partner.y) * c);
    float2 k = SignedWave(id.xy) * (Tau / CascadeLength[id.z]);
    float2 imaginaryHeight = float2(-height.y, height.x);
    float2 dx = imaginaryHeight * (k.x * mode.w);
    float2 dz = imaginaryHeight * (k.y * mode.w);
    float2 sx = imaginaryHeight * k.x;
    float2 sz = imaginaryHeight * k.y;
    float2 deriv = -height * mode.w;
    float2 dxx = deriv * k.x * k.x;
    float2 dzz = deriv * k.y * k.y;
    float2 dxz = deriv * k.x * k.y;
    WriteA[id] = float4(height.x - dxz.y, dx.x - dz.y, sx.x - sz.y, dxx.x - dzz.y);
    WriteB[id] = float4(height.y + dxz.x, dx.y + dz.x, sx.y + sz.x, dxx.y + dzz.x);
}

groupshared float4 FftReal[256];
groupshared float4 FftImaginary[256];

void InverseTransform(uint2 group, uint lane, bool columns)
{
    if (lane < WaveSize)
    {
        uint3 position = columns ? uint3(group.x, lane, group.y) : uint3(lane, group.x, group.y);
        uint index = reversebits(lane) >> (32u - WaveLog2);
        FftReal[index] = ReadA.Load(int4(position, 0));
        FftImaginary[index] = ReadB.Load(int4(position, 0));
    }
    GroupMemoryBarrierWithGroupSync();
    for (uint halfPeriod = 1u; halfPeriod < WaveSize; halfPeriod <<= 1u)
    {
        if (lane < WaveSize / 2u)
        {
            uint element = lane & (halfPeriod - 1u);
            uint a = ((lane - element) << 1u) + element;
            uint b = a + halfPeriod;
            float s, c;
            sincos(element * Pi / halfPeriod, s, c);
            float4 br = FftReal[b] * c - FftImaginary[b] * s;
            float4 bi = FftReal[b] * s + FftImaginary[b] * c;
            float4 ar = FftReal[a];
            float4 ai = FftImaginary[a];
            FftReal[a] = ar + br;
            FftImaginary[a] = ai + bi;
            FftReal[b] = ar - br;
            FftImaginary[b] = ai - bi;
        }
        GroupMemoryBarrierWithGroupSync();
    }
    if (lane < WaveSize)
    {
        uint3 position = columns ? uint3(group.x, lane, group.y) : uint3(lane, group.x, group.y);
        WriteA[position] = FftReal[lane];
        WriteB[position] = FftImaginary[lane];
    }
}

[numthreads(256, 1, 1)]
void RowsCS(uint3 group : SV_GroupID, uint lane : SV_GroupIndex)
{
    InverseTransform(group.xy, lane, false);
}

[numthreads(256, 1, 1)]
void ColumnsCS(uint3 group : SV_GroupID, uint lane : SV_GroupIndex)
{
    InverseTransform(group.xy, lane, true);
}

[numthreads(8, 8, 1)]
void ResolveCS(uint3 id : SV_DispatchThreadID)
{
    float4 a = ReadA.Load(int4(id, 0));
    float4 b = ReadB.Load(int4(id, 0));
    float2 derivative = float2(a.w, b.w) * Surface.z;
    float cross = b.x * Surface.z;
    float jacobian = (1.0 + derivative.x) * (1.0 + derivative.y) - cross * cross;
    float foam = 1.0 - smoothstep(0.16, 0.84, jacobian);
    if (Surface.w > 0.0)
        foam = max(foam, FoamHistory.Load(int4(id, 0)).z * Surface.w);
    WriteA[id] = float4(a.y * Surface.z, a.x * Surface.y, b.y * Surface.z, jacobian);
    WriteB[id] = float4(float2(a.z, b.z) / (1.0 + abs(derivative)) * Surface.y, saturate(foam), 0.0);
}

float Cell(int2 p)
{
    return (Hash(asuint(p.x) * 0x9e3779b1u ^ asuint(p.y) * 0x85ebca6bu) & 0xffffffu) / 16777215.0;
}

float Noise(float2 p)
{
    int2 i = int2(floor(p));
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(Cell(i), Cell(i + int2(1, 0)), f.x),
        lerp(Cell(i + int2(0, 1)), Cell(i + 1), f.x), f.y);
}

float CloudDensity(float2 p)
{
    float2x2 rotation = float2x2(0.84, -0.54, 0.54, 0.84);
    float baseShape = Noise(p * 0.61 + float2(11.4, -3.5));
    float form = Noise(p) * 0.504;
    p = mul(rotation, p) * 2.08 + 14.3;
    form += Noise(p) * 0.252;
    p = mul(rotation, p) * 2.12 - 8.2;
    form += Noise(p) * 0.131;
    p = mul(rotation, p) * 2.09 + 5.7;
    form += Noise(p) * 0.067 + Noise(p * 2.05) * 0.037;
    float threshold = lerp(0.70, 0.32, Atmosphere.x);
    return smoothstep(threshold - 0.055, threshold + 0.20, form * 0.83 + baseShape * 0.17);
}

float3 Sky(float3 ray, bool detailed, bool sunDisk)
{
    float elevation = max(ray.y, 0.0);
    float3 sky = lerp(SkyHorizon.rgb, SkyTop.rgb, 1.0 - exp(-6.4 * elevation));
    float alignment = saturate(dot(ray, Sun.xyz));
    float3 sunTint = float3(1.0, 0.89, 0.73);
    float daylight = smoothstep(-0.045, 0.075, Sun.y) * Atmosphere.z;
    float air = 0.19 * pow(alignment, 12.0) + 0.27 * pow(alignment, 76.0);
    float solar = smoothstep(0.99950, 0.99984, alignment) * 6.0 + pow(alignment, 1150.0) * 0.93;
    sky += sunTint * (air * daylight);
    if (detailed && elevation > 0.025 && Atmosphere.x > 0.0)
    {
        float2 xy = ray.xz * (3.6 / (elevation + 0.078)) + float2(5.15, 9.8);
        xy = mul(float2x2(0.94, 0.34, -0.34, 0.94), xy) * float2(0.90, 1.1);
        float amount = CloudDensity(xy) * smoothstep(0.027, 0.12, elevation);
        float thick = CloudDensity(xy - Sun.xz * 0.26);
        float shade = 0.76 + 0.30 * saturate((amount - thick) * 4.0 + 0.46);
        float3 cream = lerp(float3(0.67, 0.77, 0.87), float3(1.14, 1.10, 1.03), shade);
        sky = lerp(sky, cream + sunTint * air * 0.35, amount * 0.92);
        solar *= 1.0 - 0.56 * amount;
    }
    return sky + (sunDisk ? sunTint * (solar * daylight) : 0.0);
}

float SampleWeight(float footprint, float length)
{
    float cell = length / WaveSize;
    return 1.0 - smoothstep(cell * 0.72, cell * 2.75, footprint);
}

float HeightAt(float2 position, float footprint)
{
    float height = 0.0;
    [unroll] for (uint layer = 0; layer < 4u; layer++)
    {
        float length = CascadeLength[layer];
        height += SampleWeight(footprint, length)
            * ReadA.SampleLevel(FieldSampler, float3(position / length + 0.5 / WaveSize, layer), 0).y;
    }
    return height;
}

void SurfaceAt(float2 position, float footprint, out float3 displacement, out float2 slope, out float foam)
{
    displacement = 0;
    slope = 0;
    float clearWater = 1.0;
    [unroll] for (uint layer = 0; layer < 4u; layer++)
    {
        float length = CascadeLength[layer];
        float weight = SampleWeight(footprint, length);
        float3 uv = float3(position / length + 0.5 / WaveSize, layer);
        float4 wave = ReadA.SampleLevel(FieldSampler, uv, 0);
        float3 detail = ReadB.SampleLevel(FieldSampler, uv, 0).xyz;
        displacement += wave.xyz * weight;
        slope += detail.xy * weight;
        clearWater *= 1.0 - detail.z * weight;
    }
    foam = saturate((1.0 - clearWater) * Material.y);
}

float Fresnel(float mu, float roughness)
{
    const float waterIndex = 0.0200593;
    float exponent = 5.0 * exp(-2.69 * roughness);
    return waterIndex + (1.0 - waterIndex) * pow(1.0 - saturate(mu), exponent)
        / (1.0 + 22.7 * roughness * sqrt(roughness));
}

float SmithBeckmann(float cosine, float roughness)
{
    cosine = max(0.001, saturate(cosine));
    float a = cosine / max(0.00001, roughness * sqrt(1.0 - cosine * cosine));
    if (a >= 1.6) return 0.0;
    float aa = a * a;
    return (1.0 - 1.259 * a + 0.396 * aa) / (3.535 * a + 2.181 * aa);
}

float Beckmann(float nh, float roughness)
{
    nh = max(0.001, saturate(nh));
    float nh2 = nh * nh;
    float m2 = roughness * roughness;
    return exp((nh2 - 1.0) / (m2 * nh2)) / (Pi * m2 * nh2 * nh2);
}

float2 MicroSlope(float2 p, float footprint)
{
    float noise = Noise(p * 1.3);
    float3 phase = float3(dot(p, float2(6.6, 4.4)), dot(p, float2(-9.1, 7.3)),
        dot(p, float2(18.5, -12.7)));
    phase += Surface.x * (Tau / Period) * float3(109, 153, 199);
    phase += noise * float3(4.3, -2.7, 6.1);
    float3 wave = cos(phase) * (1.0 - smoothstep(float3(0.09, 0.055, 0.026),
        float3(0.33, 0.22, 0.15), footprint));
    float2 slope = float2(dot(wave, float3(0.078, -0.054, 0.036)),
        dot(wave, float3(0.050, 0.064, -0.025)));
    if (footprint < 0.065)
    {
        float2 shortPhase = float2(dot(p, float2(39.3, 30.0)), dot(p, float2(-63.0, 70.1)));
        shortPhase += Surface.x * (Tau / Period) * float2(290.0, 491.0);
        shortPhase += Noise(p * 8.6) * float2(3.0, -5.0);
        slope += sin(shortPhase) * float2(0.106, 0.079)
            * (1.0 - smoothstep(float2(0.008, 0.004), float2(0.065, 0.033), footprint));
    }
    return slope * min(1.0, Wind.x / 11.0);
}

float SeaIntersection(float3 ray, float flat, float footprint)
{
    float nearWave = smoothstep(0.012, 0.12, -ray.y);
    if (nearWave < 0.008 || Surface.y <= 0.001)
        return flat;

    // Search from the nearest possible crest toward the trough. A fixed-point
    // solve at the undisplaced plane can converge to an occluded back face.
    float reach = min(22.0, min(Eye.y * 1.25, 1.90 * Surface.y) / max(0.055, -ray.y)) * nearWave;
    float start = max(0.06, flat - reach);
    uint steps = HighQuality != 0u ? 8u : 5u;
    float stride = (flat + reach - start) / steps;
    float before = Eye.y + ray.y * start - HeightAt(Eye.xz + ray.xz * start, footprint) * nearWave;
    [loop] for (uint stepIndex = 1u; stepIndex <= steps; stepIndex++)
    {
        float afterPosition = start + stepIndex * stride;
        float after = Eye.y + ray.y * afterPosition
            - HeightAt(Eye.xz + ray.xz * afterPosition, footprint) * nearWave;
        if (before >= 0.0 && after <= 0.0)
        {
            float fraction = before / max(0.00001, before - after);
            return afterPosition - stride * (1.0 - fraction);
        }
        before = after;
    }
    return flat;
}

float3 Water(float3 ray)
{
    float downward = -ray.y;
    float flat = min(2600.0, Eye.y / downward);
    float footprint = flat * Eye.w / max(0.17, sqrt(downward));
    float distance = SeaIntersection(ray, flat, footprint);
    float2 hit = Eye.xz + ray.xz * distance;
    float3 displacement;
    float2 slope;
    float foam;
    SurfaceAt(hit, footprint, displacement, slope, foam);
    if (footprint < 0.56 && distance < 95.0)
    {
        hit -= 0.40 * displacement.xz;
        SurfaceAt(hit, footprint, displacement, slope, foam);
    }
    float micro = 1.0 - smoothstep(0.12, 0.62, footprint);
    float2 ripple = micro > 0.002 ? MicroSlope(hit, footprint) : 0.0;
    float3 normal = normalize(float3(-slope.x - ripple.x, 1.0, -slope.y - ripple.y));
    float3 view = -ray;
    float3 reflectDirection = reflect(ray, normal);
    float3 halfway = normalize(Sun.xyz + view + float3(0.0, 0.00001, 0.0));
    float roughness = clamp(Material.z + 0.07 * smoothstep(32.0, 445.0, distance)
        + 0.14 * foam, 0.036, 0.90);
    float nv = saturate(dot(normal, view));
    float nl = saturate(dot(normal, Sun.xyz));
    float nh = saturate(dot(normal, halfway));
    float fresnel = Fresnel(nv, roughness);
    float masking = 1.0 / (1.0 + SmithBeckmann(nv, roughness) + SmithBeckmann(nl, roughness));
    float spec = masking * Fresnel(saturate(dot(halfway, view)), roughness)
        * Beckmann(nh, roughness) * nl / max(0.045, 4.0 * Sun.y);
    float daylight = smoothstep(-0.025, 0.11, Sun.y) * Atmosphere.z;

    float3 deep = float3(0.009, 0.040, 0.065) * (0.68 + 0.53 * nl);
    float back = pow(saturate(dot(ray, Sun.xyz)), 6.0);
    float litCrest = smoothstep(-0.32, 0.77, displacement.y);
    float3 scattering = float3(0.018, 0.090, 0.106) * litCrest * (0.42 + 0.82 * back) * daylight;
    float3 reflection = Sky(reflectDirection, false, false);
    if (HighQuality != 0u && footprint < 0.29 && reflectDirection.y > 0.12)
        reflection = lerp(reflection, Sky(reflectDirection, true, false), 0.23);
    reflection *= float3(0.45, 0.61, 0.73);
    reflection = min(reflection, 1.12);
    float3 water = (deep + scattering) * (1.0 - fresnel) + reflection * fresnel;

    float crests = smoothstep(-0.56, 0.27, displacement.y + 0.57 * foam);
    float coverage = smoothstep(0.25, 0.86, foam) * crests;
    if (coverage > 0.008 && footprint < 1.2)
    {
        float2 froth = hit + displacement.xz * 0.33;
        float grain = 0.44 * Noise(froth * float2(6.4, 4.4)) + 0.35 * Noise(froth * 17.5)
            + 0.21 * Noise(froth * 56.0);
        float pockets = smoothstep(0.27, 0.65, grain) * 0.91 + 0.05;
        coverage *= lerp(pockets, 0.73, smoothstep(0.16, 1.2, footprint));
    }
    water += float3(1.0, 0.94, 0.80) * min(5.0, spec * 3.10) * daylight * (1.0 - 0.54 * coverage);
    water = lerp(water, float3(0.70, 0.81, 0.85) * (0.76 + nl * 0.34), coverage * 0.95);
    float haze = 1.0 - exp(-distance * 0.0041 * Atmosphere.y);
    return lerp(water, Sky(ray, false, false), haze);
}

float3 Display(float3 linearColor)
{
    // A soft shoulder leaves texture in the whitecaps and clips only the solar core.
    float3 mapped = saturate(1.16 * max(0.0, linearColor) / (1.0 + 0.32 * max(0.0, linearColor)));
    return lerp(12.92 * mapped, 1.055 * pow(mapped, 1.0 / 2.4) - 0.055, step(0.0031308, mapped));
}

[numthreads(8, 8, 1)]
void RenderCS(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= TargetWidth || id.y >= TargetHeight) return;
    float2 uv = (id.xy + 0.5) / float2(TargetWidth, TargetHeight);
    float3 ray = normalize(Forward.xyz + RightExtent.xyz * (2.0 * uv.x - 1.0)
        + UpExtent.xyz * (1.0 - 2.0 * uv.y));
    bool water = ray.y < -0.0007;
    float3 linearColor = water ? Water(ray) : Sky(ray, true, true);
    float alpha = (water || SkyEnabled != 0u) ? 1.0 : 0.0;
    Output[id.xy] = float4(Display(linearColor) * alpha, alpha);
}
