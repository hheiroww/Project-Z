using System.Numerics;
using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace ProjectZ.StudioScenes;

/// <summary>Owns the Direct3D 11 FFT fields and draws a premultiplied RGBA8 ocean.</summary>
internal sealed class StudioOceanGpu : IDisposable
{
    private const int Cascades = 4;
    private const float Period = 200;
    private readonly Device _device;
    private readonly DeviceContext _context;
    private readonly ComputeShader[] _kernels;
    private Buffer? _parameters;
    private SamplerState? _sampler;
    private WaveFields? _waves;
    private Texture2D? _frame;
    private Texture2D? _staging;
    private UnorderedAccessView? _frameView;
    private int[] _pixels = [];
    private (int Width, int Height) _frameSize;
    private Spectrum _spectrum;
    private double _lastSeconds = double.NaN;
    private float _lastScale;
    private float _lastChop;
    private int _foamIndex;

    private StudioOceanGpu(Device device, DeviceContext context, byte[][] shaders)
    {
        _device = device;
        _context = context;
        _kernels = new ComputeShader[shaders.Length];
        try
        {
            for (var i = 0; i < shaders.Length; i++)
                _kernels[i] = new ComputeShader(device, shaders[i]);
            _parameters = new Buffer(device, Marshal.SizeOf<OceanParameters>(),
                ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _sampler = new SamplerState(device, new SamplerStateDescription
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Clamp,
                Filter = Filter.MinMagLinearMipPoint,
                ComparisonFunction = Comparison.Never,
                MaximumLod = float.MaxValue,
                MaximumAnisotropy = 1
            });
        }
        catch
        {
            ReleaseResources();
            throw;
        }
    }

    internal static StudioOceanGpu Create(byte[][] shaders)
    {
        var device = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport, FeatureLevel.Level_11_0);
        DeviceContext? context = null;
        try
        {
            context = device.ImmediateContext;
            return new StudioOceanGpu(device, context, shaders);
        }
        catch
        {
            context?.Dispose();
            device.Dispose();
            throw;
        }
    }

    internal static StudioOceanGpu Attach(nint devicePointer, nint contextPointer, byte[][] shaders)
    {
        Marshal.AddRef(devicePointer);
        Marshal.AddRef(contextPointer);
        var device = new Device(devicePointer);
        var context = new DeviceContext(contextPointer);
        try
        {
            if (device.FeatureLevel < FeatureLevel.Level_11_0)
                throw new NotSupportedException("GPU ocean requires Direct3D feature level 11.0.");
            return new StudioOceanGpu(device, context, shaders);
        }
        catch
        {
            context.Dispose();
            device.Dispose();
            throw;
        }
    }

    internal bool Uses(nint device, nint immediateContext) =>
        _device.NativePointer == device && _context.NativePointer == immediateContext;

    internal nint Draw(int width, int height, double seconds, StudioScene scene,
        StudioCameraPose pose, StudioSignal signal, bool highQuality)
    {
        var ocean = scene.Ocean;
        var waveSize = highQuality ? Math.Min(256, ocean.FftResolution * 2) : ocean.FftResolution;
        var requested = new Spectrum(waveSize, ocean.Seed, ocean.WindSpeed, ocean.WindDirection,
            ocean.Fetch, ocean.Depth, ocean.Swell);
        bool initialize = _waves is null || requested != _spectrum;
        if (initialize)
        {
            if (_waves?.Size != waveSize)
            {
                _waves?.Dispose();
                _waves = new WaveFields(_device, waveSize);
            }
            _spectrum = requested;
            _lastSeconds = double.NaN;
            _foamIndex = 0;
        }
        Resize(width, height);
        var elapsed = seconds - _lastSeconds;
        var persistence = elapsed >= 0 && elapsed < 0.42 ? MathF.Exp(-1.65f * (float)elapsed) : 0;
        var constants = Parameters(width, height, waveSize, seconds, persistence, scene, pose, signal, highQuality);
        _context.UpdateSubresource(ref constants, _parameters!);
        var compute = _context.ComputeShader;
        compute.SetConstantBuffer(0, _parameters);
        compute.SetSampler(0, _sampler);
        try
        {
            var fields = _waves!;
            var tile = waveSize / 8;
            if (initialize)
                Dispatch(0, tile, tile, Cascades, writeA: fields.Initial.Output);
            if (seconds != _lastSeconds || constants.Surface.Y != _lastScale || constants.Surface.Z != _lastChop)
            {
                Dispatch(1, tile, tile, Cascades, fields.Initial.Input, writeA: fields.WorkA.Output, writeB: fields.WorkB.Output);
                Dispatch(2, waveSize, Cascades, 1, fields.WorkA.Input, fields.WorkB.Input,
                    writeA: fields.ScratchA.Output, writeB: fields.ScratchB.Output);
                Dispatch(3, waveSize, Cascades, 1, fields.ScratchA.Input, fields.ScratchB.Input,
                    writeA: fields.WorkA.Output, writeB: fields.WorkB.Output);
                int next = 1 - _foamIndex;
                Dispatch(4, tile, tile, Cascades, fields.WorkA.Input, fields.WorkB.Input, fields.Surface[_foamIndex].Input,
                    fields.Displacement.Output, fields.Surface[next].Output);
                _foamIndex = next;
                _lastSeconds = seconds;
                _lastScale = constants.Surface.Y;
                _lastChop = constants.Surface.Z;
            }
            Dispatch(5, (width + 7) / 8, (height + 7) / 8, 1,
                fields.Displacement.Input, fields.Surface[_foamIndex].Input, color: _frameView);
        }
        finally
        {
            compute.Set(null);
            compute.SetSampler(0, null);
            compute.SetConstantBuffer(0, null);
        }
        return _frame!.NativePointer;
    }

    private void Dispatch(int kernel, int x, int y, int z, ShaderResourceView? readA = null,
        ShaderResourceView? readB = null, ShaderResourceView? history = null,
        UnorderedAccessView? writeA = null, UnorderedAccessView? writeB = null, UnorderedAccessView? color = null)
    {
        var compute = _context.ComputeShader;
        compute.Set(_kernels[kernel]);
        if (readA is not null) compute.SetShaderResource(0, readA);
        if (readB is not null) compute.SetShaderResource(1, readB);
        if (history is not null) compute.SetShaderResource(2, history);
        if (writeA is not null) compute.SetUnorderedAccessView(0, writeA);
        if (writeB is not null) compute.SetUnorderedAccessView(1, writeB);
        if (color is not null) compute.SetUnorderedAccessView(2, color);
        try
        {
            _context.Dispatch(x, y, z);
        }
        finally
        {
            if (readA is not null) compute.SetShaderResource(0, null);
            if (readB is not null) compute.SetShaderResource(1, null);
            if (history is not null) compute.SetShaderResource(2, null);
            if (writeA is not null) compute.SetUnorderedAccessView(0, null);
            if (writeB is not null) compute.SetUnorderedAccessView(1, null);
            if (color is not null) compute.SetUnorderedAccessView(2, null);
        }
    }

    internal unsafe ReadOnlySpan<int> CopyPixels()
    {
        var (width, height) = _frameSize;
        _staging ??= new Texture2D(_device, Texture(width, height, 1, Format.R8G8B8A8_UNorm,
            ResourceUsage.Staging, BindFlags.None, CpuAccessFlags.Read));
        if (_pixels.Length != checked(width * height))
            _pixels = new int[width * height];
        _context.CopyResource(_frame, _staging);
        var mapping = _context.MapSubresource(_staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
        try
        {
            var pixels = MemoryMarshal.Cast<int, uint>(_pixels.AsSpan());
            for (var y = 0; y < height; y++)
                RgbaToBgra(new ReadOnlySpan<uint>((byte*)mapping.DataPointer + y * mapping.RowPitch, width),
                    pixels.Slice(y * width, width));
        }
        finally
        {
            _context.UnmapSubresource(_staging, 0);
        }
        return _pixels;
    }

    private static void RgbaToBgra(ReadOnlySpan<uint> source, Span<uint> destination)
    {
        static uint Swap(uint rgba) => (rgba & 0xff00ff00) | ((rgba & 0xff) << 16) | ((rgba >> 16) & 0xff);
        var index = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var mask = new Vector<uint>(0xff00ff00);
            var channel = new Vector<uint>(0xff);
            for (; index <= source.Length - Vector<uint>.Count; index += Vector<uint>.Count)
            {
                var rgba = new Vector<uint>(source[index..]);
                ((rgba & mask) | ((rgba & channel) << 16) | ((rgba >> 16) & channel)).CopyTo(destination[index..]);
            }
        }
        for (; index < source.Length; index++)
            destination[index] = Swap(source[index]);
    }

    private void Resize(int width, int height)
    {
        if (_frameSize == (width, height) && _frameView is not null)
            return;
        _frameView?.Dispose();
        _frameView = null;
        _frame?.Dispose();
        _staging?.Dispose();
        _staging = null;
        _frame = new Texture2D(_device, Texture(width, height, 1, Format.R8G8B8A8_UNorm,
            ResourceUsage.Default, BindFlags.ShaderResource | BindFlags.UnorderedAccess, CpuAccessFlags.None));
        _frameView = new UnorderedAccessView(_device, _frame);
        _frameSize = (width, height);
    }

    private static OceanParameters Parameters(int width, int height, int size, double seconds, float persistence,
        StudioScene scene, StudioCameraPose pose, StudioSignal signal, bool quality)
    {
        var ocean = scene.Ocean;
        var (sy, cy) = MathF.SinCos(Radians(pose.Camera.Yaw));
        var (sp, cp) = MathF.SinCos(Radians(pose.Camera.Pitch));
        var (sr, cr) = MathF.SinCos(Radians(pose.Camera.Roll));
        var forward = new Vector3(-sy * cp, -sp, -cy * cp);
        var right = new Vector3(cy, 0, -sy);
        var up = Vector3.Cross(right, forward);
        var scale = MathF.Tan(Radians(pose.Camera.FieldOfView) / 2) / pose.Camera.Zoom;
        var (ss, cs) = MathF.SinCos(Radians(ocean.SunElevation));
        var (sa, ca) = MathF.SinCos(Radians(ocean.SunAzimuth));
        var scaleFromSound = ocean.WaveScale * (1 + ocean.AudioResponse * (.48f * signal.Bass + .52f * signal.Beat));
        return new OceanParameters
        {
            Width = (uint)width, Height = (uint)height, Size = (uint)size, Log2Size = (uint)BitOperations.Log2((uint)size),
            Seed = unchecked((uint)ocean.Seed), HighQuality = quality ? 1u : 0u, SkyEnabled = scene.Sky.Enabled ? 1u : 0u,
            Surface = new((float)Math.IEEERemainder(seconds, Period), scaleFromSound, ocean.Choppiness * scaleFromSound, persistence),
            Wind = new(ocean.WindSpeed, Radians(ocean.WindDirection), ocean.Fetch, ocean.Depth),
            Material = new(ocean.Swell, ocean.Foam, ocean.Roughness, 0),
            Eye = new(pose.X, MathF.Max(.35f, ocean.EyeHeight + pose.Y), pose.Z, 2 * scale / height),
            Forward = new(forward, 0),
            RightExtent = new((right * cr + up * sr) * (scale * width / height), 0),
            UpExtent = new((up * cr - right * sr) * scale, 0),
            Sun = new(-sa * cs, ss, -ca * cs, 0),
            SkyTop = new(Linear(scene.Sky.TopColor), 0),
            SkyHorizon = new(Linear(scene.Sky.HorizonColor), 0),
            Atmosphere = new(ocean.CloudCoverage, ocean.Haze, ocean.SunIntensity, 0)
        };
    }

    private static float Radians(float degrees) => degrees * MathF.PI / 180;

    private static Vector3 Linear(int argb)
    {
        static float Channel(int code)
        {
            var value = (code & 255) / 255f;
            return value <= .04045f ? value / 12.92f : MathF.Pow((value + .055f) / 1.055f, 2.4f);
        }
        return new(Channel(argb >>> 16), Channel(argb >>> 8), Channel(argb));
    }

    private static Texture2DDescription Texture(int width, int height, int layers, Format format,
        ResourceUsage usage, BindFlags bind, CpuAccessFlags access) => new()
    {
        Width = width, Height = height, MipLevels = 1, ArraySize = layers, Format = format,
        SampleDescription = new SampleDescription(1, 0), Usage = usage, BindFlags = bind, CpuAccessFlags = access
    };

    private void ReleaseResources()
    {
        _waves?.Dispose();
        _frameView?.Dispose();
        _frame?.Dispose();
        _staging?.Dispose();
        _parameters?.Dispose();
        _sampler?.Dispose();
        foreach (var kernel in _kernels)
            kernel?.Dispose();
    }

    public void Dispose()
    {
        ReleaseResources();
        _context.Dispose();
        _device.Dispose();
    }

    private readonly record struct Spectrum(int Size, int Seed, float Speed, float Direction, float Fetch, float Depth, float Swell);

    [StructLayout(LayoutKind.Sequential)]
    private struct OceanParameters
    {
        internal uint Width, Height, Size, Log2Size;
        internal uint Seed, HighQuality, SkyEnabled, Padding;
        internal Vector4 Surface, Wind, Material, Eye, Forward, RightExtent, UpExtent, Sun, SkyTop, SkyHorizon, Atmosphere;
    }

    private sealed class Field : IDisposable
    {
        private readonly Texture2D _texture;
        internal ShaderResourceView Input { get; }
        internal UnorderedAccessView Output { get; }

        internal Field(Device device, int size)
        {
            _texture = new Texture2D(device, Texture(size, size, Cascades, Format.R32G32B32A32_Float,
                ResourceUsage.Default, BindFlags.ShaderResource | BindFlags.UnorderedAccess, CpuAccessFlags.None));
            try
            {
                Input = new ShaderResourceView(device, _texture);
                Output = new UnorderedAccessView(device, _texture);
            }
            catch
            {
                Input?.Dispose();
                _texture.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Output.Dispose();
            Input.Dispose();
            _texture.Dispose();
        }
    }

    private sealed class WaveFields : IDisposable
    {
        private readonly Field[] _fields = new Field[8];
        internal int Size { get; }
        internal Field Initial => _fields[0];
        internal Field WorkA => _fields[1];
        internal Field WorkB => _fields[2];
        internal Field ScratchA => _fields[3];
        internal Field ScratchB => _fields[4];
        internal Field Displacement => _fields[5];
        internal ArraySegment<Field> Surface => new(_fields, 6, 2);

        internal WaveFields(Device device, int size)
        {
            Size = size;
            try
            {
                for (var i = 0; i < _fields.Length; i++)
                    _fields[i] = new Field(device, size);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            foreach (var field in _fields)
                field?.Dispose();
        }
    }
}
