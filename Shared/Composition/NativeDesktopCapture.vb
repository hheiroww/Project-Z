Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework

Namespace [Shared].Composition
    Public Class DesktopCaptureUnavailableException
        Inherits InvalidOperationException
        Public ReadOnly Property State As DesktopBackdropState
        Public Sub New(state As DesktopBackdropState, message As String)
            MyBase.New(message)
            Me.State = state
        End Sub
    End Class

    Friend Module DesktopNativeWindow
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function WmInfoDelegate(window As IntPtr, info As IntPtr) As Integer
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Sub VersionDelegate(version As IntPtr)
        Friend Function Resolve(game As Game) As IntPtr
#If WINDOWS Then
            Return Global.ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(game)
#Else
            Dim name = If(OperatingSystem.IsMacOS(), "libSDL2-2.0.0.dylib", If(OperatingSystem.IsWindows(), "SDL2.dll", "libSDL2-2.0.so.0"))
            Dim library = NativeLibrary.Load(name, GetType(Game).Assembly, Nothing)
            Dim buffer = Marshal.AllocHGlobal(256)
            Try
                For index = 0 To 255
                    Marshal.WriteByte(buffer, index, 0)
                Next
                Marshal.GetDelegateForFunctionPointer(Of VersionDelegate)(NativeLibrary.GetExport(library, "SDL_GetVersion"))(buffer)
                Dim read = Marshal.GetDelegateForFunctionPointer(Of WmInfoDelegate)(NativeLibrary.GetExport(library, "SDL_GetWindowWMInfo"))
                If read(game.Window.Handle, buffer) = 0 Then Return IntPtr.Zero
                Dim subsystem = Marshal.ReadInt32(buffer, 4)
                If subsystem = 2 Then Return Marshal.ReadIntPtr(buffer, 8 + IntPtr.Size) ' X11 Window after Display*.
                If subsystem = 1 OrElse subsystem = 4 Then Return Marshal.ReadIntPtr(buffer, 8) ' HWND / NSWindow*.
                Return IntPtr.Zero
            Finally
                Marshal.FreeHGlobal(buffer)
                NativeLibrary.Free(library)
            End Try
#End If
        End Function
    End Module

    Friend Module DesktopCaptureFactory
        Friend Function Create(nativeWindow As IntPtr) As IDesktopCaptureSource
            If OperatingSystem.IsLinux() Then
                If String.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) OrElse
                   Not String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) Then
                    Throw New DesktopCaptureUnavailableException(DesktopBackdropState.SessionChangeRequired,
                        "Desktop blur needs an X11 session or a compositor provider that excludes this window. Installing X11 does not change an active Wayland session.")
                End If
                Return New X11DesktopCapture(nativeWindow)
            End If
            If OperatingSystem.IsWindows() AndAlso Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) Then
                Throw New DesktopCaptureUnavailableException(DesktopBackdropState.Unsupported, "Desktop blur requires Windows 10 version 2004 or later for window exclusion.")
            End If
            If OperatingSystem.IsMacOS() AndAlso Not OperatingSystem.IsMacOSVersionAtLeast(13) Then
                Throw New DesktopCaptureUnavailableException(DesktopBackdropState.Unsupported, "Desktop blur requires macOS 13 or later and Screen Recording permission.")
            End If
            If OperatingSystem.IsWindows() OrElse OperatingSystem.IsMacOS() Then Return New NativeDesktopCapture(nativeWindow)
            Throw New DesktopCaptureUnavailableException(DesktopBackdropState.Unsupported, "No desktop capture provider is installed for this platform.")
        End Function
    End Module

    Friend NotInheritable Class NativeDesktopCapture
        Implements IDesktopCaptureSource
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function CreateDelegate(window As IntPtr) As IntPtr
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function BoundsDelegate(context As IntPtr, ByRef x As Integer, ByRef y As Integer, ByRef width As Integer, ByRef height As Integer, ByRef scale As Single) As Integer
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function ReadDelegate(context As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, downsample As Integer, <Out> rgba As Byte(), capacity As Integer) As Integer
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Sub DestroyDelegate(context As IntPtr)
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function ErrorDelegate() As IntPtr
        Private ReadOnly library As IntPtr
        Private Shared loadedLibrary As IntPtr
        Private Shared ReadOnly libraryGate As New Object
        Private Shared Function LoadLibrary() As IntPtr
            SyncLock libraryGate
                If loadedLibrary = IntPtr.Zero Then loadedLibrary = NativeLibrary.Load(If(OperatingSystem.IsWindows(), "ProjectZDesktopCapture.dll", "libProjectZDesktopCapture.dylib"), GetType(NativeDesktopCapture).Assembly, Nothing)
                Return loadedLibrary
            End SyncLock
        End Function
        Private ReadOnly read As ReadDelegate
        Private ReadOnly bounds As BoundsDelegate
        Private ReadOnly destroy As DestroyDelegate
        Private ReadOnly errorText As ErrorDelegate
        Private context As IntPtr
        Private scale As Single = 1
        Friend Sub New(window As IntPtr)
            Try
                library = LoadLibrary()
                read = Marshal.GetDelegateForFunctionPointer(Of ReadDelegate)(NativeLibrary.GetExport(library, "pz_capture_read"))
                bounds = Marshal.GetDelegateForFunctionPointer(Of BoundsDelegate)(NativeLibrary.GetExport(library, "pz_capture_bounds"))
                destroy = Marshal.GetDelegateForFunctionPointer(Of DestroyDelegate)(NativeLibrary.GetExport(library, "pz_capture_destroy"))
                errorText = Marshal.GetDelegateForFunctionPointer(Of ErrorDelegate)(NativeLibrary.GetExport(library, "pz_capture_error"))
                Dim create = Marshal.GetDelegateForFunctionPointer(Of CreateDelegate)(NativeLibrary.GetExport(library, "pz_capture_create"))
                context = create(window)
                If context = IntPtr.Zero Then
                    Dim message = Marshal.PtrToStringUTF8(errorText())
                    Throw New DesktopCaptureUnavailableException(If(OperatingSystem.IsMacOS(), DesktopBackdropState.PermissionRequired, DesktopBackdropState.Failed), message)
                End If
            Catch ex As DllNotFoundException
                Throw New DesktopCaptureUnavailableException(DesktopBackdropState.DependenciesRequired, "The platform desktop-capture library is missing: " & ex.Message)
            End Try
        End Sub
        Public ReadOnly Property ExcludesHostWindow As Boolean Implements IDesktopCaptureSource.ExcludesHostWindow
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property WindowBounds As Rectangle Implements IDesktopCaptureSource.WindowBounds
            Get
                Dim x, y, width, height As Integer
                If context = IntPtr.Zero OrElse bounds(context, x, y, width, height, scale) = 0 Then Return Rectangle.Empty
                Return New Rectangle(x, y, width, height)
            End Get
        End Property
        Public ReadOnly Property DpiScale As Single Implements IDesktopCaptureSource.DpiScale
            Get
                Return scale
            End Get
        End Property
        Public Function TryCapture(area As Rectangle, downsample As Integer, rgba As Byte()) As Boolean Implements IDesktopCaptureSource.TryCapture
            Dim result = read(context, area.X, area.Y, area.Width, area.Height, downsample, rgba, rgba.Length)
            If result < 0 Then Throw New InvalidOperationException(Marshal.PtrToStringUTF8(errorText()))
            Return result = 1
        End Function
        Public Sub Dispose() Implements IDisposable.Dispose
            If context = IntPtr.Zero Then Return
            destroy(context)
            context = IntPtr.Zero
            ' Keep the module loaded: ScreenCaptureKit can finish asynchronous teardown after this call.
        End Sub
    End Class
End Namespace
