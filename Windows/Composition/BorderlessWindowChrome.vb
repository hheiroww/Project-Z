Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Windows.Composition
    ''' <summary>Native client sizing, resize hit tests and cursors for borderless HWNDs.</summary>
    Public NotInheritable Class BorderlessWindowChrome
        Inherits NativeWindow
        Implements IDisposable

        Private Const NearestMonitor As UInteger = 2UI
        Private Const NoSize As UInteger = 1UI
        Private Const NoMove As UInteger = 2UI
        Private ReadOnly resizable As Boolean
        Private disposed As Boolean

        Public Sub New(hwnd As IntPtr, Optional canResize As Boolean = True)
            If hwnd = IntPtr.Zero Then Throw New ArgumentException("A native HWND is required.", NameOf(hwnd))
            resizable = canResize
            AssignHandle(hwnd)
        End Sub

        Public Shared Function ResizeHitTest(point As Global.System.Drawing.Point, size As Global.System.Drawing.Size,
                                             Optional border As Integer = 8, Optional corner As Integer = 16) As Integer
            If point.X < 0 OrElse point.Y < 0 OrElse point.X >= size.Width OrElse point.Y >= size.Height Then Return 1
            Dim left = point.X < corner, right = point.X >= size.Width - corner
            Dim top = point.Y < corner, bottom = point.Y >= size.Height - corner
            If top AndAlso left Then Return 13
            If top AndAlso right Then Return 14
            If bottom AndAlso left Then Return 16
            If bottom AndAlso right Then Return 17
            If point.X < border Then Return 10
            If point.X >= size.Width - border Then Return 11
            If point.Y < border Then Return 12
            If point.Y >= size.Height - border Then Return 15
            Return 1
        End Function

        Public Shared Function CursorForHitTest(hit As Integer) As Cursor
            Select Case hit
                Case 10, 11 : Return Cursors.SizeWE
                Case 12, 15 : Return Cursors.SizeNS
                Case 13, 17 : Return Cursors.SizeNWSE
                Case 14, 16 : Return Cursors.SizeNESW
                Case Else : Return Cursors.Default
            End Select
        End Function

        Protected Overrides Sub WndProc(ByRef message As Message)
            Select Case message.Msg
                Case &H24 ' WM_GETMINMAXINFO: preserve the host's tracking limits.
                    MyBase.WndProc(message)
                    If resizable AndAlso message.LParam <> IntPtr.Zero Then
                        Dim monitor As NativeMonitorInfo
                        If TryMonitorInfo(MonitorFromWindow(Handle, NearestMonitor), monitor) Then
                            Dim limits = Marshal.PtrToStructure(Of NativeMinMaxInfo)(message.LParam)
                            limits.MaxPosition = New NativePoint With {.X = monitor.WorkArea.Left - monitor.Bounds.Left,
                                                                       .Y = monitor.WorkArea.Top - monitor.Bounds.Top}
                            limits.MaxSize = New NativePoint With {.X = monitor.WorkArea.Right - monitor.WorkArea.Left,
                                                                   .Y = monitor.WorkArea.Bottom - monitor.WorkArea.Top}
                            Marshal.StructureToPtr(limits, message.LParam, False)
                            message.Result = IntPtr.Zero
                        End If
                    End If
                    Return
                Case &H46 ' WM_WINDOWPOSCHANGING
                    MyBase.WndProc(message)
                    If resizable AndAlso message.LParam <> IntPtr.Zero AndAlso IsZoomed(Handle) Then
                        FitMaximizedWindow(message.LParam)
                    End If
                    Return
                Case &H83 ' WM_NCCALCSIZE: keep the whole HWND as client pixels.
                    message.Result = IntPtr.Zero
                    Return
                Case &H84 ' WM_NCHITTEST; preserve the host's caption/input policy.
                    If resizable AndAlso Not IsZoomed(Handle) Then
                        Dim packed = message.LParam.ToInt64()
                        Dim bytes = BitConverter.GetBytes(packed)
                        Dim point As New Global.System.Drawing.Point(BitConverter.ToInt16(bytes, 0), BitConverter.ToInt16(bytes, 2))
                        Dim bounds As New NativeRect
                        GetWindowRect(Handle, bounds)
                        point.Offset(-bounds.Left, -bounds.Top)
                        Dim hit = ResizeHitTest(point, New Global.System.Drawing.Size(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top))
                        If hit <> 1 Then message.Result = New IntPtr(hit) : Return
                    End If
                Case &H20 ' WM_SETCURSOR, including forwarded per-pixel surface messages.
                    Dim hit = CInt(message.LParam.ToInt64() And &HFFFF)
                    If resizable AndAlso hit >= 10 AndAlso hit <= 17 AndAlso Not IsZoomed(Handle) Then
                        SetCursor(CursorForHitTest(hit).Handle)
                        message.Result = New IntPtr(1)
                        Return
                    End If
            End Select
            MyBase.WndProc(message)
        End Sub

        Private Shared Function TryMonitorInfo(monitor As IntPtr, ByRef info As NativeMonitorInfo) As Boolean
            info.Size = CUInt(Marshal.SizeOf(Of NativeMonitorInfo)())
            Return GetMonitorInfo(monitor, info)
        End Function

        Private Sub FitMaximizedWindow(positionPtr As IntPtr)
            Dim position = Marshal.PtrToStructure(Of NativeWindowPosition)(positionPtr)
            Dim canMove = (position.Flags And NoMove) = 0UI
            Dim canSize = (position.Flags And NoSize) = 0UI
            If Not canMove AndAlso Not canSize Then Return

            Dim monitor = If(canMove, MonitorFromPoint(New NativePoint With {.X = position.X, .Y = position.Y}, NearestMonitor),
                             MonitorFromWindow(Handle, NearestMonitor))
            Dim info As NativeMonitorInfo
            If Not TryMonitorInfo(monitor, info) Then Return
            ' Windows may adjust WM_GETMINMAXINFO again for a secondary display.
            ' Enforce the target monitor's actual work area after that translation.
            If canMove Then
                position.X = info.WorkArea.Left
                position.Y = info.WorkArea.Top
            End If
            If canSize Then
                position.Width = info.WorkArea.Right - info.WorkArea.Left
                position.Height = info.WorkArea.Bottom - info.WorkArea.Top
            End If
            Marshal.StructureToPtr(position, positionPtr, False)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            ReleaseHandle()
        End Sub

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativePoint
            Public X, Y As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeMinMaxInfo
            Public Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize As NativePoint
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeWindowPosition
            Public Window, InsertAfter As IntPtr
            Public X, Y, Width, Height As Integer
            Public Flags As UInteger
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeMonitorInfo
            Public Size As UInteger
            Public Bounds, WorkArea As NativeRect
            Public Flags As UInteger
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeRect
            Public Left, Top, Right, Bottom As Integer
        End Structure
        <DllImport("user32.dll")> Private Shared Function MonitorFromWindow(hwnd As IntPtr, flags As UInteger) As IntPtr
        End Function
        <DllImport("user32.dll")> Private Shared Function MonitorFromPoint(point As NativePoint, flags As UInteger) As IntPtr
        End Function
        <DllImport("user32.dll", EntryPoint:="GetMonitorInfoW")>
        Private Shared Function GetMonitorInfo(monitor As IntPtr, ByRef info As NativeMonitorInfo) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function GetWindowRect(hwnd As IntPtr, ByRef rect As NativeRect) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function IsZoomed(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function SetCursor(cursor As IntPtr) As IntPtr
        End Function
    End Class
End Namespace
