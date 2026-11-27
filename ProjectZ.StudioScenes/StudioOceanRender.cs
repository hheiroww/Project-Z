using System.Numerics;

namespace ProjectZ.StudioScenes;

/// <summary>Draws the shared FFT ocean in linear light, including its reflected sky, scattering and foam.</summary>
internal sealed class StudioOceanRender
{
    private static readonly Vector3 Up = Vector3.UnitY;
    private static readonly Vector3 FoamColor = new(.79f, .87f, .92f);
    private static readonly Vector3 DeepWater = new(.006f, .050f, .076f);
    private readonly SpectralOcean _waves = new();
    private int[] _pixels = [];

    public ReadOnlySpan<int> Draw(int width, int height, double seconds, StudioScene scene,
        StudioCameraPose pose, StudioSignal signal, bool highQuality)
    {
        var ocean = scene.Ocean;
        _waves.Update(ocean, seconds, signal, highQuality);
        if (_pixels.Length != checked(width * height))
            _pixels = new int[width * height];

        var (sy, cy) = MathF.SinCos(Radians(pose.Camera.Yaw));
        var (sp, cp) = MathF.SinCos(Radians(pose.Camera.Pitch));
        var (sr, cr) = MathF.SinCos(Radians(pose.Camera.Roll));
        var forward = new Vector3(-sy * cp, -sp, -cy * cp);
        var right = new Vector3(cy, 0, -sy);
        var up = Vector3.Cross(right, forward);
        var scale = MathF.Tan(Radians(pose.Camera.FieldOfView) / 2) / pose.Camera.Zoom;
        var horizontal = (right * cr + up * sr) * scale * width / height;
        var vertical = (up * cr - right * sr) * scale;
        var eye = new Vector3(pose.X, MathF.Max(.35f, ocean.EyeHeight + pose.Y), pose.Z);
        var (ss, cs) = MathF.SinCos(Radians(ocean.SunElevation));
        var (sa, ca) = MathF.SinCos(Radians(ocean.SunAzimuth));
        var sun = new Vector3(-sa * cs, ss, -ca * cs);
        var sky = new OceanSky(scene.Sky, sun);
        var footprint = 2 * scale / height;
        var roughness = ocean.Roughness;
        var workers = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount - 1, 1, 12) };
        Parallel.For(0, height, workers, y =>
        {
            var row = forward + vertical * (1 - (2 * (y + .5f) / height));
            for (var x = 0; x < width; x++)
            {
                var ray = Vector3.Normalize(row + horizontal * (2 * (x + .5f) / width - 1));
                var light = ray.Y < -.0008f
                    ? ShadeWater(ray, eye, sun, sky, footprint, roughness, highQuality)
                    : sky.Color(ray, clouds: true);
                _pixels[y * width + x] = ray.Y >= -.0008f && !scene.Sky.Enabled ? 0 : ToBgra(light);
            }
        });
        return _pixels;
    }

    private Vector3 ShadeWater(Vector3 ray, Vector3 eye, Vector3 sun, OceanSky sky, float pixelAngle,
        float roughness, bool highQuality)
    {
        float fall = -ray.Y;
        float planeHit = MathF.Min(3800, eye.Y / fall);
        float distance = planeHit;
        // In a grazing view a small crest moves the intersection a long way. Limit the search to its
        // local footprint, where the selected cascades still have resolved geometry.
        float correction = MathF.Min(planeHit * .42f, 22f);
        float nearWave = Smooth(.018f, .145f, fall);
        float footprint = planeHit * pixelAngle / MathF.Max(.14f, MathF.Sqrt(fall));
        for (var step = 0; step < (highQuality ? 2 : 1); step++)
        {
            var pos = eye + ray * distance;
            var height = _waves.SampleHeight(pos.X, pos.Z, footprint);
            distance += (Math.Clamp(planeHit - height / MathF.Max(.06f, fall) * nearWave,
                planeHit - correction, planeHit + correction) - distance) * .66f;
        }

        var hit = eye + ray * distance;
        var wave = _waves.Sample(hit.X, hit.Z, footprint);
        // The field describes displaced world positions. Invert its horizontal offset once to keep
        // a crest's normal, spray and parallax aligned with the same displaced surface.
        if (highQuality && footprint < .52f)
            wave = _waves.Sample(hit.X - .59f * wave.Displacement.X, hit.Z - .59f * wave.Displacement.Z, footprint);
        var normal = Vector3.Normalize(Vector3.Lerp(Up, wave.Normal, 1 - .24f * Smooth(.16f, 1.8f, footprint)));
        var view = -ray;
        var reflected = Vector3.Reflect(ray, normal);
        var foam = Math.Clamp(wave.Foam, 0, 1);
        var foamCoverage = Smooth(.29f, .88f, foam) * Smooth(-.15f, .37f, wave.Height + foam * .32f);
        if (foamCoverage > .015f && footprint < 1.25f)
        {
            var frothPoint = new Vector2(hit.X, hit.Z) + new Vector2(wave.Displacement.X, wave.Displacement.Z);
            var bubbles = .70f * Noise(frothPoint * 2.9f) + .30f * Noise(frothPoint * 8.4f);
            foamCoverage *= Lerp(.50f + .48f * Smooth(.20f, .74f, bubbles), .72f, Smooth(.07f, 1.25f, footprint));
        }
        var surfaceRoughness = Math.Clamp(roughness + .14f * foam + .062f * Smooth(32, 420, distance), .025f, .90f);
        var fresnel = Fresnel(Vector3.Dot(normal, view), surfaceRoughness);

        // Water/FFTWater.shader by Garrett Gunnell, MIT. Modified for Project-Z.
        // The companion spectral field supplies displacement, slope and determinant-derived foam.
        var sum = sun + view;
        var halfway = sum.LengthSquared() > .000001f ? Vector3.Normalize(sum) : normal;
        var diffuseLight = MathF.Max(0, Vector3.Dot(normal, sun));
        var specularFresnel = Fresnel(Vector3.Dot(halfway, view), surfaceRoughness);
        var masking = 1 / (1 + SmithBeckmann(Vector3.Dot(normal, view), surfaceRoughness)
            + SmithBeckmann(diffuseLight, surfaceRoughness));
        var highlight = MathF.Min(10, specularFresnel * masking * Beckmann(Vector3.Dot(normal, halfway), surfaceRoughness)
            * diffuseLight / (4 * MathF.Max(.015f, sun.Y))) * Smooth(-.03f, .10f, sun.Y);
        var sunWhite = new Vector3(1f, .91f, .77f);
        var crest = MathF.Max(0, wave.Height);
        var backlight = MathF.Pow(MathF.Max(0, Vector3.Dot(-view, sun)), 4);
        var transmitted = DeepWater * (.42f + .66f * diffuseLight)
            + new Vector3(.017f, .081f, .101f) * (crest / (1 + crest) * (.45f + .64f * backlight));
        var reflection = sky.Color(reflected, clouds: highQuality && footprint < .56f) * new Vector3(.56f, .77f, .90f);
        transmitted *= .73f + .33f * Smooth(-.45f, .47f, wave.Height);
        var water = transmitted * (1 - fresnel) + reflection * fresnel + sunWhite * (highlight * 1.75f);
        water = Vector3.Lerp(water, FoamColor * (.37f + .57f * diffuseLight), foamCoverage * .93f);
        float fog = 1 - MathF.Exp(-distance * .00265f);
        return Vector3.Lerp(water, sky.Color(ray, clouds: false), fog);
    }

    private static float Fresnel(float normalDotEye, float roughness)
    {
        const float waterReflectance = .020059312f;
        var mu = Math.Clamp(normalDotEye, 0, 1);
        return waterReflectance + (1 - waterReflectance) * MathF.Pow(1 - mu, 5 * MathF.Exp(-2.69f * roughness))
            / (1 + 22.7f * roughness * MathF.Sqrt(roughness));
    }

    private static float SmithBeckmann(float cosine, float roughness)
    {
        cosine = Math.Clamp(cosine, .001f, 1);
        var a = cosine / MathF.Max(.00001f, roughness * MathF.Sqrt(1 - cosine * cosine));
        if (a >= 1.6f)
            return 0;
        float a2 = a * a;
        return (1 - 1.259f * a + .396f * a2) / (3.535f * a + 2.181f * a2);
    }

    private static float Beckmann(float normalDotHalf, float roughness)
    {
        float cosine = Math.Clamp(normalDotHalf, .001f, 1);
        float c2 = cosine * cosine;
        float a2 = roughness * roughness;
        return MathF.Exp((c2 - 1) / (a2 * c2)) / (MathF.PI * a2 * c2 * c2);
    }

    private readonly struct OceanSky
    {
        private readonly Vector3 _horizon;
        private readonly Vector3 _zenith;
        private readonly Vector3 _sun;

        public OceanSky(StudioSky settings, Vector3 sun)
        {
            _horizon = Linear(settings.HorizonColor);
            _zenith = Linear(settings.TopColor);
            _sun = sun;
        }

        public Vector3 Color(Vector3 ray, bool clouds)
        {
            float elevation = MathF.Max(ray.Y, 0);
            float atmosphere = Smooth(.012f, .76f, MathF.Sqrt(elevation));
            var color = Vector3.Lerp(_horizon, _zenith, atmosphere);
            if (clouds && elevation > .022f)
            {
                var projection = new Vector2(ray.X, ray.Z) * (1.8f / (elevation + .16f)) + new Vector2(-2.7f, 5.3f);
                var cloud = .61f * Noise(projection * 1.9f) + .26f * Noise(projection * 4.9f)
                    + .13f * Noise(projection * 11.7f);
                var coverage = Smooth(.44f, .72f, cloud) * Smooth(.025f, .18f, elevation);
                color = Vector3.Lerp(color, new Vector3(.66f, .73f, .81f), coverage * .9f);
            }
            float sunFacing = Math.Clamp(Vector3.Dot(ray, _sun), 0, 1);
            var haze = MathF.Pow(sunFacing, 38) * .21f;
            var disk = Smooth(.99925f, .99984f, sunFacing);
            return color + new Vector3(1f, .86f, .69f) * (haze + disk * 2.85f);
        }
    }

    private static float Noise(Vector2 point)
    {
        int x = (int)MathF.Floor(point.X), y = (int)MathF.Floor(point.Y);
        var dx = Smooth(0, 1, point.X - x);
        var dy = Smooth(0, 1, point.Y - y);
        return Lerp(Lerp(Hash(x, y), Hash(x + 1, y), dx), Lerp(Hash(x, y + 1), Hash(x + 1, y + 1), dx), dy);
    }

    private static float Hash(int x, int y)
    {
        uint bits = unchecked((uint)x * 0x9e37_79b1u ^ (uint)y * 0x85eb_ca6bu);
        bits ^= bits >> 16;
        bits = unchecked(bits * 0x7feb_352du);
        bits ^= bits >> 15;
        return (bits & 0xffffff) / 16777215f;
    }

    private static Vector3 Linear(int argb)
    {
        var encoded = new Vector3((argb >>> 16) & 255, (argb >>> 8) & 255, argb & 255) / 255f;
        return encoded * encoded;
    }

    private static int ToBgra(Vector3 color)
    {
        static int Srgb(float linear) => (int)(MathF.Sqrt(Math.Clamp(linear, 0f, 1f)) * 255f + .5f);
        return unchecked((int)0xff000000) | Srgb(color.X) << 16 | Srgb(color.Y) << 8 | Srgb(color.Z);
    }

    private static float Smooth(float start, float end, float value)
    {
        var t = Math.Clamp((value - start) / (end - start), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Radians(float degrees) => degrees * MathF.PI / 180;
}
