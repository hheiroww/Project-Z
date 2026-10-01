Imports System.Runtime.CompilerServices
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing
    Public Enum TextureFiltering
        Bilinear
        Trilinear
        Anisotropic2x
        Anisotropic4x
        Anisotropic8x
        Anisotropic16x
    End Enum

    ' One configuration per device, shared by all of its scenes and tool windows.
    Public NotInheritable Class GraphicsQuality
        Private Shared ReadOnly devices As New ConditionalWeakTable(Of GraphicsDevice, GraphicsQuality)
        Private ReadOnly samplers As New Dictionary(Of TextureFiltering, SamplerState)
        Private ReadOnly sampleCounts As New Dictionary(Of Integer, Integer)
        Private requestedSamples As Integer
        Friend Property RenderScale As Vector2 = Vector2.One
        Public ReadOnly Property AntiAliasingTechnique As String
            Get
#If PROJECTZ_DIRECTX12 Then
                ' MonoGame 3.8.5.1's native DX12 render-target allocation ignores MSAA.
                Return "SSAA"
#Else
                Return "MSAA"
#End If
            End Get
        End Property
        Friend Function ScaleRectangle(value As Rectangle) As Rectangle
            Return New Rectangle(CInt(value.X * RenderScale.X), CInt(value.Y * RenderScale.Y),
                CInt(value.Width * RenderScale.X), CInt(value.Height * RenderScale.Y))
        End Function
        Public Property Filtering As TextureFiltering = TextureFiltering.Anisotropic16x

        Public Shared Function ForDevice(device As GraphicsDevice) As GraphicsQuality
            Return devices.GetValue(device, Function(key)
                Dim quality As New GraphicsQuality()
                AddHandler key.Disposing, Sub(sender, args)
                    For Each cachedSampler As SamplerState In quality.samplers.Values
                        cachedSampler.Dispose()
                    Next
                    quality.samplers.Clear()
                End Sub
                Return quality
            End Function)
        End Function

        Public Property AntiAliasingSamples As Integer
            Get
                Return requestedSamples
            End Get
            Set(value As Integer)
                If Not {0, 2, 4, 8, 16}.Contains(value) Then Throw New ArgumentOutOfRangeException(NameOf(value))
                requestedSamples = value
            End Set
        End Property

        Public ReadOnly Property Sampler As SamplerState
            Get
                If Not [Enum].IsDefined(GetType(TextureFiltering), Filtering) Then Throw New ArgumentOutOfRangeException(NameOf(Filtering))
                Dim result As SamplerState = Nothing
                If Not samplers.TryGetValue(Filtering, result) Then
                    result = New SamplerState With {.AddressU = TextureAddressMode.Clamp, .AddressV = TextureAddressMode.Clamp,
                        .AddressW = TextureAddressMode.Clamp, .Filter = TextureFilter.Linear, .MaxAnisotropy = 1}
                    If Filtering = TextureFiltering.Bilinear Then result.Filter = TextureFilter.LinearMipPoint
                    If Filtering >= TextureFiltering.Anisotropic2x Then
                        result.Filter = TextureFilter.Anisotropic
                        result.MaxAnisotropy = 1 << (CInt(Filtering) - 1)
                    End If
                    samplers.Add(Filtering, result)
                End If
                Return result
            End Get
        End Property

        Public Function SupportedSamples(device As GraphicsDevice, requested As Integer) As Integer
            If requested = 0 Then Return 0
            If Not {2, 4, 8, 16}.Contains(requested) Then Throw New ArgumentOutOfRangeException(NameOf(requested))
#If PROJECTZ_DIRECTX12 Then
            Return requested
#Else
            Dim cached As Integer
            If sampleCounts.TryGetValue(requested, cached) Then Return cached
            Dim format As SurfaceFormat, depth As DepthFormat, samples As Integer
            device.Adapter.QueryRenderTargetFormat(device.GraphicsProfile, SurfaceFormat.Color,
                DepthFormat.Depth24Stencil8, requested, format, depth, samples)
            Using target As New RenderTarget2D(device, 8, 8, False, SurfaceFormat.Color, DepthFormat.Depth24Stencil8,
                samples, RenderTargetUsage.DiscardContents)
                cached = If(target.MultiSampleCount > 1, target.MultiSampleCount, 0)
            End Using
            sampleCounts.Add(requested, cached)
            Return cached
#End If
        End Function
    End Class
End Namespace
