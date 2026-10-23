Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Windows.Composition
    ''' <summary>Native client sizing, resize hit tests and cursors for borderless HWNDs.</summary>
    Public NotInheritable Class BorderlessWindowChrome
        Inherits NativeWindow
        Implements IDisposable

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

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            ReleaseHandle()
        End Sub

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeRect
            Public Left, Top, Right, Bottom As Integer
        End Structure
        <DllImport("user32.dll")> Private Shared Function GetWindowRect(hwnd As IntPtr, ByRef rect As NativeRect) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function IsZoomed(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")> Private Shared Function SetCursor(cursor As IntPtr) As IntPtr
        End Function
    End Class
End Namespace
