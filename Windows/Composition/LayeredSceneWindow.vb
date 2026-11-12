Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework
Imports Forms = System.Windows.Forms

Namespace Windows.Composition
    ''' <summary>Composites Project-Z's GPU frame into a Win32 per-pixel alpha surface.</summary>
    Public NotInheritable Class LayeredSceneWindow
        Inherits Forms.Form

        Private dc, bitmap, originalBitmap, bits As IntPtr
        Private buffer As Integer()
        Private bitmapWidth, bitmapHeight As Integer
        Private ReadOnly captionHeight, captionButtonsWidth As Integer
        Private ReadOnly resizable As Boolean

        Public Sub New(title As String, width As Integer, height As Integer, captionHeight As Integer, captionButtonsWidth As Integer, minWidth As Integer, minHeight As Integer,
                       Optional resizable As Boolean = True)
            Text = title
            Me.captionHeight = captionHeight
            Me.captionButtonsWidth = captionButtonsWidth
            Me.resizable = resizable
            AutoScaleMode = Forms.AutoScaleMode.None
            FormBorderStyle = Forms.FormBorderStyle.None
            StartPosition = Forms.FormStartPosition.CenterScreen
            ClientSize = New Global.System.Drawing.Size(width, height)
            MinimumSize = New Global.System.Drawing.Size(minWidth, minHeight)
        End Sub

        Protected Overrides ReadOnly Property CreateParams As Forms.CreateParams
            Get
                Dim options = MyBase.CreateParams
                options.ExStyle = options.ExStyle Or &H80000 ' WS_EX_LAYERED
                If resizable Then options.Style = options.Style Or &H40000 ' WS_THICKFRAME
                Return options
            End Get
        End Property

        Protected Overrides Function IsInputKey(keyData As Forms.Keys) As Boolean
            Return True
        End Function

        Protected Overrides Sub WndProc(ByRef message As Forms.Message)
            If message.Msg = &H83 AndAlso resizable Then ' WM_NCCALCSIZE
                message.Result = IntPtr.Zero
                Return
            End If
            If message.Msg = &H20 AndAlso resizable AndAlso WindowState = Forms.FormWindowState.Normal Then
                Dim cursorHit = CInt(message.LParam.ToInt64() And &HFFFF)
                If cursorHit >= 10 AndAlso cursorHit <= 17 Then
                    Forms.Cursor.Current = BorderlessWindowChrome.CursorForHitTest(cursorHit)
                    message.Result = New IntPtr(1)
                    Return
                End If
            End If
            MyBase.WndProc(message)
            If message.Msg <> &H84 OrElse message.Result <> New IntPtr(1) Then Return
            Dim coordinates = BitConverter.GetBytes(message.LParam.ToInt64())
            Dim point = PointToClient(New Global.System.Drawing.Point(BitConverter.ToInt16(coordinates, 0), BitConverter.ToInt16(coordinates, 2)))
            If resizable AndAlso WindowState = Forms.FormWindowState.Normal Then
                Dim hit = BorderlessWindowChrome.ResizeHitTest(point, ClientSize, border:=6)
                If hit <> 1 Then
                    message.Result = New IntPtr(hit)
                    Return
                End If
            End If
            If point.Y < captionHeight AndAlso point.X < ClientSize.Width - captionButtonsWidth Then message.Result = New IntPtr(2) ' HTCAPTION
        End Sub

        Public Sub Present(pixels As Color(), width As Integer, height As Integer)
            ArgumentNullException.ThrowIfNull(pixels)
            If width <= 0 OrElse height <= 0 OrElse pixels.LongLength <> CLng(width) * height Then
                Throw New ArgumentException("Frame dimensions do not match the pixel buffer.", NameOf(pixels))
            End If
            If width <> bitmapWidth OrElse height <> bitmapHeight Then Allocate(width, height)
            For index = 0 To buffer.Length - 1
                Dim value = pixels(index).PackedValue
                Dim bgra = (value And &HFF00FF00UI) Or ((value And &HFFUI) << 16) Or ((value >> 16) And &HFFUI)
                buffer(index) = CInt(bgra And &H7FFFFFFFUI)
                If (bgra And &H80000000UI) <> 0 Then buffer(index) = buffer(index) Or Integer.MinValue
            Next
            Marshal.Copy(buffer, 0, bits, buffer.Length)
            Dim destination As New NativePoint With {.X = Left, .Y = Top}
            Dim source As New NativePoint()
            Dim size As New NativeSize With {.Width = width, .Height = height}
            Dim blend As New BlendFunction With {.SourceConstantAlpha = 255, .AlphaFormat = 1}
            If Not UpdateLayeredWindow(Handle, IntPtr.Zero, destination, size, dc, source, 0, blend, 2) Then
                Throw New Win32Exception(Marshal.GetLastWin32Error(), "Could not present the Project-Z frame.")
            End If
        End Sub

        Private Sub Allocate(width As Integer, height As Integer)
            ReleaseDib()
            dc = CreateCompatibleDC(IntPtr.Zero)
            Dim header As New BitmapHeader With {.Size = CUInt(Marshal.SizeOf(Of BitmapHeader)()), .Width = width, .Height = -height, .Planes = 1US, .BitCount = 32US}
            bitmap = CreateDIBSection(dc, header, 0, bits, IntPtr.Zero, 0)
            If dc = IntPtr.Zero OrElse bitmap = IntPtr.Zero Then
                Dim errorCode = Marshal.GetLastWin32Error()
                ReleaseDib()
                Throw New Win32Exception(errorCode, "Could not allocate the Project-Z presentation bitmap.")
            End If
            originalBitmap = SelectObject(dc, bitmap)
            bitmapWidth = width : bitmapHeight = height
            buffer = New Integer(width * height - 1) {}
        End Sub

        Private Sub ReleaseDib()
            If originalBitmap <> IntPtr.Zero Then SelectObject(dc, originalBitmap)
            If bitmap <> IntPtr.Zero Then DeleteObject(bitmap)
            If dc <> IntPtr.Zero Then DeleteDC(dc)
            originalBitmap = IntPtr.Zero : bitmap = IntPtr.Zero : dc = IntPtr.Zero : bits = IntPtr.Zero
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then ReleaseDib()
            MyBase.Dispose(disposing)
        End Sub

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativePoint
            Public X, Y As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeSize
            Public Width, Height As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure BitmapHeader
            Public Size As UInteger
            Public Width, Height As Integer
            Public Planes, BitCount As UShort
            Public Compression, SizeImage As UInteger
            Public XResolution, YResolution As Integer
            Public ColorsUsed, ImportantColors As UInteger
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure BlendFunction
            Public BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat As Byte
        End Structure

        <DllImport("gdi32.dll", SetLastError:=True)>
        Private Shared Function CreateCompatibleDC(hdc As IntPtr) As IntPtr
        End Function

        <DllImport("gdi32.dll", SetLastError:=True)>
        Private Shared Function CreateDIBSection(hdc As IntPtr, ByRef header As BitmapHeader, usage As UInteger, ByRef bits As IntPtr, section As IntPtr, offset As UInteger) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function SelectObject(hdc As IntPtr, value As IntPtr) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteObject(value As IntPtr) As Boolean
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteDC(hdc As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function UpdateLayeredWindow(hwnd As IntPtr, destinationDC As IntPtr, ByRef destination As NativePoint, ByRef size As NativeSize,
            sourceDC As IntPtr, ByRef source As NativePoint, colorKey As UInteger, ByRef blend As BlendFunction, flags As UInteger) As Boolean
        End Function
    End Class
End Namespace
