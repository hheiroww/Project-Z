Imports System.Runtime.InteropServices
Imports System.Runtime.CompilerServices
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
        Private inSizeMove, rendering, timerStarted, disposed As Boolean

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
                        If inSizeMove AndAlso Not rendering AndAlso Not disposed AndAlso isEnabled() AndAlso
                           IsWindowVisible(Handle) AndAlso Not IsIconic(Handle) Then
                            If Interlocked.CompareExchange(sharedFrameGate.Rendering, 1, 0) = 0 Then
                                rendering = True
                                Try
                                    ' The timer runs on the HWND's owning thread.
                                    ' App-owned ordinary frame entry must also opt
                                    ' out while its Update/Draw is already active.
                                    renderFrame()
                                Finally
                                    rendering = False
                                    Volatile.Write(sharedFrameGate.Rendering, 0)
                                End Try
                            End If
                        End If
                        message.Result = IntPtr.Zero
                        Return
                    End If
            End Select
            MyBase.WndProc(message)
        End Sub

        Private Sub StopTimer()
            inSizeMove = False
            If timerStarted AndAlso Handle <> IntPtr.Zero Then KillTimer(Handle, timerId)
            timerStarted = False
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
    End Class
End Namespace
