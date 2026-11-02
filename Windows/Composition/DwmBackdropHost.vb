Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework

Namespace Windows.Composition
    ''' <summary>Compatibility binding for the shader desktop backdrop. No companion HWND or DWM blur is created.</summary>
    Public NotInheritable Class DwmBackdropHost
        Implements IDisposable
        Private ReadOnly owner As IntPtr
        Private disposed As Boolean
        Public ReadOnly Property Composition As WindowCompositionResult
        Public ReadOnly Property Handle As IntPtr
            Get
                Return If(disposed, IntPtr.Zero, owner)
            End Get
        End Property
        Public ReadOnly Property IsVisible As Boolean
            Get
                Return Not disposed AndAlso IsWindowVisible(owner) AndAlso Not IsIconic(owner)
            End Get
        End Property
        Public ReadOnly Property Bounds As Global.System.Drawing.Rectangle
            Get
                Dim rect As NativeRect
                If disposed OrElse Not GetWindowRect(owner, rect) Then Return Global.System.Drawing.Rectangle.Empty
                Return Global.System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
            End Get
        End Property
        Public ReadOnly Property NativePositionUpdateCount As Long
        Public Property ProfileMask As Boolean
        Public ReadOnly Property MaskScanMilliseconds As Double
        Public ReadOnly Property MaskApplyMilliseconds As Double
        Public ReadOnly Property TransparencyMaskFrameCount As Long
        Public ReadOnly Property TransparencyMaskUpdateCount As Long
        Public ReadOnly Property TransparencyMaskApplied As Boolean
        Public ReadOnly Property TransparencyMaskRectangleCount As Integer
        Public ReadOnly Property TransparencyMaskErrorCode As Integer
        Public ReadOnly Property LastPositionSucceeded As Boolean
            Get
                Return Not disposed AndAlso IsWindow(owner)
            End Get
        End Property
        Public Sub New(owner As IntPtr, tint As Color)
            If owner = IntPtr.Zero OrElse Not IsWindow(owner) Then Throw New ArgumentException("A native owner HWND is required.", NameOf(owner))
            Me.owner = owner
            ' The shader paints the desktop behind the UI into the existing swap chain.
            ' Remove the old color-key region rather than creating a second window.
            SetWindowRgn(owner, IntPtr.Zero, False)
            SetMaterial(True, tint)
        End Sub
        Public Sub SetMaterial(enabled As Boolean, tint As Color)
            If disposed Then Return
            WindowsComposition.DisableNativeBackdrop(owner)
            _Composition = New WindowCompositionResult With {.Handle = owner, .ShaderBackdropRequested = enabled}
            Global.ProjectZ.Shared.Composition.DesktopBackdropRequests.SetEnabled(owner, enabled, tint)
        End Sub
        ''' <summary>Retained for existing callers. Shader-backed windows no longer need color-key mask scans.</summary>
        Public Sub UpdateOwnerTransparencyMask(pixels As Color(), width As Integer, height As Integer, transparentColor As Color)
            If Not disposed Then _TransparencyMaskFrameCount += 1
        End Sub
        Public Sub Tick()
            ' Position follows the owner directly; there is no companion window to synchronize.
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            Global.ProjectZ.Shared.Composition.DesktopBackdropRequests.SetEnabled(owner, False)
        End Sub
        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeRect
            Public Left, Top, Right, Bottom As Integer
        End Structure
        <DllImport("user32.dll")> Private Shared Function GetWindowRect(hwnd As IntPtr, ByRef rect As NativeRect) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function IsWindowVisible(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function IsIconic(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function SetWindowRgn(hwnd As IntPtr, region As IntPtr, redraw As Boolean) As Integer
        End Function
    End Class
End Namespace
