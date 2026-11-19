Imports System.Runtime.InteropServices
Imports System.Runtime.CompilerServices
Imports System.Diagnostics
Imports System.Threading
Imports System.Windows.Forms

Namespace Windows.Composition
    ''' <summary>Keeps the shader material presenting while Win32 owns a caption/resize loop.</summary>
    Friend NotInheritable Class DesktopBackdropWindowLoop
        Inherits NativeWindow
        Implements IDisposable

        Private ReadOnly isEnabled As Func(Of Boolean)
        Private ReadOnly renderFrame As Action
        Private ReadOnly useExistingFramePump As Boolean
        Private NotInheritable Class FrameGate
            Friend Rendering As Integer
        End Class
        Private Shared ReadOnly frameOwners As New ConditionalWeakTable(Of Object, FrameGate)
        Private ReadOnly sharedFrameGate As FrameGate
        Private ReadOnly timerId As New UIntPtr(&H505A424CUI)
        Private Shared ReadOnly geometryFrameMessage As Integer = RegisterWindowMessage("ProjectZ.DesktopBackdrop.GeometryFrame")
        Private inSizeMove, rendering, timerStarted, disposed As Boolean
        Private geometryFramePosted As Boolean
        Private geometryFrameDeadline As Long

        Friend Sub New(hwnd As IntPtr, isEnabled As Func(Of Boolean), renderFrame As Action,
                       Optional useExistingFramePump As Boolean = False, Optional frameOwner As Object = Nothing)
            If hwnd = IntPtr.Zero Then Throw New ArgumentException("A native HWND is required.", NameOf(hwnd))
            Me.isEnabled = isEnabled
            Me.renderFrame = renderFrame
            Me.useExistingFramePump = useExistingFramePump
            ' All HWNDs driven by one Game share the same dispatch gate. An HWND
            ' callback must not reenter that Game through a different host timer.
            sharedFrameGate = frameOwners.GetValue(If(frameOwner, Me), Function(owner) New FrameGate())
            AssignHandle(hwnd)
        End Sub

        Protected Overrides Sub WndProc(ByRef message As Message)
            If geometryFrameMessage <> 0 AndAlso message.Msg = geometryFrameMessage Then
                geometryFramePosted = False
                Dim started = Stopwatch.GetTimestamp()
                If TryRenderFrame() Then geometryFrameDeadline = started + Stopwatch.Frequency \ 60
                message.Result = IntPtr.Zero
                Return
            End If
            Select Case message.Msg
                Case &H231 ' WM_ENTERSIZEMOVE
                    inSizeMove = True
                    ' WindowsDX already pumps its own Game.Window. A separate
                    ' native presentation HWND still needs this timer on DX11.
                    ' Arm even if the app is temporarily busy at entry. The
                    ' callback checks readiness again on every timer message.
                    If Not useExistingFramePump Then
                        timerStarted = SetTimer(Handle, timerId, 16UI, IntPtr.Zero) <> UIntPtr.Zero
                    End If
                Case &H232, &H82 ' WM_EXITSIZEMOVE / WM_NCDESTROY
                    StopTimer()
                Case &H113 ' WM_TIMER
                    If timerStarted AndAlso message.WParam.ToInt64() = CLng(timerId.ToUInt64()) Then
                        TryRenderFrame()
                        message.Result = IntPtr.Zero
                        Return
                    End If
            End Select
            MyBase.WndProc(message)
            ' Geometry messages can keep low-priority WM_TIMER from surfacing
            ' during a drag. Queue one frame after the host consumes its bounds.
            If message.Msg = &H47 AndAlso inSizeMove AndAlso Not useExistingFramePump AndAlso Not rendering AndAlso
               Not geometryFramePosted AndAlso geometryFrameMessage <> 0 AndAlso Stopwatch.GetTimestamp() >= geometryFrameDeadline Then
                geometryFramePosted = PostMessage(Handle, geometryFrameMessage, IntPtr.Zero, IntPtr.Zero)
            End If
        End Sub

        Private Function TryRenderFrame() As Boolean
            If Not inSizeMove OrElse rendering OrElse disposed OrElse Stopwatch.GetTimestamp() < geometryFrameDeadline OrElse
               Not isEnabled() OrElse Not IsWindowVisible(Handle) OrElse IsIconic(Handle) Then Return False
            If Interlocked.CompareExchange(sharedFrameGate.Rendering, 1, 0) <> 0 Then Return False
            rendering = True
            Try
                renderFrame()
                Return True
            Finally
                rendering = False
                Volatile.Write(sharedFrameGate.Rendering, 0)
            End Try
        End Function

        Private Sub StopTimer()
            inSizeMove = False
            If timerStarted AndAlso Handle <> IntPtr.Zero Then KillTimer(Handle, timerId)
            timerStarted = False
            geometryFramePosted = False
            geometryFrameDeadline = 0
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            StopTimer()
            ReleaseHandle()
        End Sub

        <DllImport("user32.dll", ExactSpelling:=True)>
        Private Shared Function SetTimer(hwnd As IntPtr, id As UIntPtr, milliseconds As UInteger, callback As IntPtr) As UIntPtr
        End Function
        <DllImport("user32.dll", ExactSpelling:=True)>
        Private Shared Function KillTimer(hwnd As IntPtr, id As UIntPtr) As Boolean
        End Function
        <DllImport("user32.dll", ExactSpelling:=True)>
        Private Shared Function IsWindowVisible(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll", ExactSpelling:=True)>
        Private Shared Function IsIconic(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function RegisterWindowMessage(name As String) As Integer
        End Function
        <DllImport("user32.dll", EntryPoint:="PostMessageW", ExactSpelling:=True)>
        Private Shared Function PostMessage(hwnd As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As Boolean
        End Function
    End Class
End Namespace
