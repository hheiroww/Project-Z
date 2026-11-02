Imports Microsoft.Xna.Framework

Namespace [Shared].Composition
    Public Enum DesktopBackdropState
        Disabled
        Starting
        Active
        PermissionRequired
        DependenciesRequired
        SessionChangeRequired
        Unsupported
        Failed
    End Enum

    ''' <summary>Capture implementations must omit the host window, not return a black placeholder for it.
    ''' Read runs on a worker thread. Bounds is read on the game thread. RGBA pixels remain in memory.</summary>
    Public Interface IDesktopCaptureSource
        Inherits IDisposable
        ReadOnly Property ExcludesHostWindow As Boolean
        ReadOnly Property WindowBounds As Rectangle
        ReadOnly Property DpiScale As Single
        Function TryCapture(bounds As Rectangle, downsample As Integer, rgba As Byte()) As Boolean
    End Interface

    Public NotInheritable Class DesktopBackdropSettings
        Public Property Enabled As Boolean
        ''' <summary>Gaussian standard deviation in desktop logical pixels.</summary>
        Public Property BlurRadius As Single = 20
        Public Property CaptureFramesPerSecond As Integer = 30
        Public Property Saturation As Single = 1.05F
        Public Property NoiseOpacity As Single = 0.006F
        Public Property Tint As Color = Color.Transparent
        Public Property FallbackColor As Color = New Color(24, 28, 32)
    End Class

    ''' <summary>Legacy Windows appearance APIs request the shared shader through this registry.</summary>
    Public Module DesktopBackdropRequests
        Friend NotInheritable Class Request
            Friend ReadOnly Enabled As Boolean
            Friend ReadOnly Tint As Color
            Friend Sub New(enabled As Boolean, tint As Color)
                Me.Enabled = enabled : Me.Tint = tint
            End Sub
        End Class
        Private ReadOnly requests As New System.Collections.Concurrent.ConcurrentDictionary(Of IntPtr, Request)
        Public Sub SetEnabled(nativeWindow As IntPtr, enabled As Boolean, Optional tint As Color = Nothing)
            If nativeWindow = IntPtr.Zero Then Return
            Dim value As New Request(enabled, tint)
            requests.AddOrUpdate(nativeWindow, value, Function(key, previous) If(previous.Enabled = enabled AndAlso previous.Tint = tint, previous, value))
        End Sub
        Public Sub Remove(nativeWindow As IntPtr)
            Dim ignored As Request = Nothing
            requests.TryRemove(nativeWindow, ignored)
        End Sub
        Friend Function TryGet(nativeWindow As IntPtr, ByRef value As Request) As Boolean
            Return requests.TryGetValue(nativeWindow, value)
        End Function
    End Module
End Namespace
