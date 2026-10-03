Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports Microsoft.Xna.Framework

Namespace Windows.Composition
    ''' <summary>
    ''' Acrylic companion HWND for renderers whose DXGI swap chain ignores alpha.
    ''' Project-Z clips unused client pixels from the DX12 HWND with a native window
    ''' region; this window occupies the same screen rectangle immediately behind it
    ''' and supplies the real DWM material visible through those pixels.
    ''' </summary>
    Public NotInheritable Class DwmBackdropHost
        Implements IDisposable

        Private Const SwpNoActivate As UInteger = &H10UI
        Private Const SwpShowWindow As UInteger = &H40UI
        Private ReadOnly owner As IntPtr
        Private ReadOnly backdrop As Form
        Private ReadOnly ownerWindow As OwnerWindowObserver
        Private synchronizing As Boolean
        Public ReadOnly Property NativePositionUpdateCount As Long
        Private disposed As Boolean
        Private lastBounds As Global.System.Drawing.Rectangle
        Private lastVisible As Boolean
        Private lastMaskRectangles As Global.System.Drawing.Rectangle() = Array.Empty(Of Global.System.Drawing.Rectangle)()
        Private lastMaskClientSize As Microsoft.Xna.Framework.Point
        Private ReadOnly maskBands As MaskBand() = Enumerable.Range(0, Math.Min(4, Environment.ProcessorCount)).Select(Function(index) New MaskBand()).ToArray()
        Private ReadOnly maskParallelOptions As New Threading.Tasks.ParallelOptions With {.MaxDegreeOfParallelism = 4}
        Private regionData As Integer() = Array.Empty(Of Integer)()
        Public Property ProfileMask As Boolean
        Public ReadOnly Property MaskScanMilliseconds As Double
        Public ReadOnly Property MaskApplyMilliseconds As Double

        Private Class MaskBand
            Public ReadOnly Rectangles As New List(Of Global.System.Drawing.Rectangle)()
            Public Previous As New Dictionary(Of (Left As Integer, Right As Integer), Integer)()
            Public Current As New Dictionary(Of (Left As Integer, Right As Integer), Integer)()
        End Class
        Public ReadOnly Property TransparencyMaskFrameCount As Long
        Public ReadOnly Property TransparencyMaskUpdateCount As Long

        Public ReadOnly Property Composition As WindowCompositionResult
        Public ReadOnly Property Handle As IntPtr
            Get
                Return If(backdrop Is Nothing OrElse backdrop.IsDisposed, IntPtr.Zero, backdrop.Handle)
            End Get
        End Property
        Public ReadOnly Property IsVisible As Boolean
            Get
                Return backdrop IsNot Nothing AndAlso Not backdrop.IsDisposed AndAlso backdrop.Visible
            End Get
        End Property
        Public ReadOnly Property Bounds As Global.System.Drawing.Rectangle
            Get
                Return If(backdrop Is Nothing OrElse backdrop.IsDisposed,
                          Global.System.Drawing.Rectangle.Empty, backdrop.Bounds)
            End Get
        End Property
        Public Property LastPositionSucceeded As Boolean
            Get
                Return _lastPositionSucceeded
            End Get
            Private Set(value As Boolean)
                _lastPositionSucceeded = value
            End Set
        End Property
        Private _lastPositionSucceeded As Boolean
        Public Property TransparencyMaskApplied As Boolean
            Get
                Return _transparencyMaskApplied
            End Get
            Private Set(value As Boolean)
                _transparencyMaskApplied = value
            End Set
        End Property
        Private _transparencyMaskApplied As Boolean
        Public Property TransparencyMaskRectangleCount As Integer
            Get
                Return _transparencyMaskRectangleCount
            End Get
            Private Set(value As Integer)
                _transparencyMaskRectangleCount = value
            End Set
        End Property
        Private _transparencyMaskRectangleCount As Integer
        Public Property TransparencyMaskErrorCode As Integer
            Get
                Return _transparencyMaskErrorCode
            End Get
            Private Set(value As Integer)
                _transparencyMaskErrorCode = value
            End Set
        End Property
        Private _transparencyMaskErrorCode As Integer

        Public Sub New(owner As IntPtr, tint As Microsoft.Xna.Framework.Color)
            If owner = IntPtr.Zero Then Throw New ArgumentException("A native owner HWND is required.", NameOf(owner))
            Me.owner = owner
            ' An extended DWM frame only exposes its glass material where the
            ' client surface contributes zero-alpha black. A near-black RGB fill
            ' is opaque and was the solid background seen through the DX12 mask.
            backdrop = New Form With {
                .AutoScaleMode = AutoScaleMode.None,
                .BackColor = Global.System.Drawing.Color.Black,
                .FormBorderStyle = FormBorderStyle.None,
                .ShowInTaskbar = False,
                .StartPosition = FormStartPosition.Manual,
                .Text = "Project-Z DWM backdrop",
                .Enabled = False
            }
            backdrop.Show()
            Composition = WindowsComposition.ApplyToHandle(backdrop.Handle, New WindowCompositionOptions With {
                .Borderless = True,
                .Resizable = False,
                .Backdrop = WindowBackdrop.Acrylic,
                .Tint = tint,
                .FallbackAccentBlur = True,
                .ExtendFrameIntoClientArea = True
            })
            ownerWindow = New OwnerWindowObserver(owner, Me)
            Tick()
        End Sub

        ''' <summary>
        ''' Converts the renderer's unused color-key pixels into a real Win32
        ''' window region. DXGI flip-model swap chains are presented as opaque,
        ''' so WS_EX_LAYERED color-keying can report success while leaving the
        ''' key color on screen. A window region clips those pixels before DWM
        ''' composition and reveals the acrylic companion HWND behind them.
        ''' </summary>
        Public Sub UpdateOwnerTransparencyMask(pixels As Microsoft.Xna.Framework.Color(), width As Integer,
                                               height As Integer, transparentColor As Microsoft.Xna.Framework.Color)
            If disposed OrElse pixels Is Nothing OrElse width <= 0 OrElse height <= 0 OrElse
               pixels.Length < width * height OrElse Not IsWindow(owner) Then Return

            Dim client As NativeRect
            If Not GetClientRect(owner, client) Then Return
            Dim clientWidth = client.Right - client.Left
            Dim clientHeight = client.Bottom - client.Top
            If clientWidth <= 0 OrElse clientHeight <= 0 Then Return

            Dim scaleX = clientWidth / CDbl(width)
            Dim profileStarted = If(ProfileMask, Diagnostics.Stopwatch.GetTimestamp(), 0L)
            Dim scaleY = clientHeight / CDbl(height)
            Dim rectangles As New List(Of Global.System.Drawing.Rectangle)()
            Dim keyColor = transparentColor.PackedValue And &HFFFFFFUI
            ' Immutable pixel snapshot; bands write disjoint, reusable storage.
            ' No graphics-device, HWND or mutable scene access on these workers.
            Threading.Tasks.Parallel.For(0, maskBands.Length, maskParallelOptions,
                Sub(bandIndex)
            Dim band = maskBands(bandIndex)
            band.Rectangles.Clear()
            band.Previous.Clear()
            band.Current.Clear()
            Dim bandRectangles = band.Rectangles
            For y = height * bandIndex \ maskBands.Length To height * (bandIndex + 1) \ maskBands.Length - 1
                band.Current.Clear()
                Dim runStart = -1
                Dim rowOffset = y * width
                For x = 0 To width - 1
                    Dim pixel = pixels(rowOffset + x)
                    Dim opaque = (pixel.PackedValue And &HFFFFFFUI) <> keyColor
                    If opaque AndAlso runStart < 0 Then runStart = x
                    If runStart >= 0 AndAlso (Not opaque OrElse x = width - 1) Then
                        Dim runEnd = If(opaque AndAlso x = width - 1, x + 1, x)
                        Dim left = CInt(Math.Floor(runStart * scaleX))
                        Dim right = CInt(Math.Ceiling(runEnd * scaleX))
                        Dim top = CInt(Math.Floor(y * scaleY))
                        Dim bottom = CInt(Math.Ceiling((y + 1) * scaleY))
                        If right > left AndAlso bottom > top Then
                            Dim key = (left, right)
                            Dim index As Integer
                            If band.Previous.TryGetValue(key, index) AndAlso bandRectangles(index).Bottom >= top Then
                                Dim existing = bandRectangles(index)
                                bandRectangles(index) = Global.System.Drawing.Rectangle.FromLTRB(left, existing.Top, right, bottom)
                            Else
                                index = bandRectangles.Count
                                bandRectangles.Add(Global.System.Drawing.Rectangle.FromLTRB(left, top, right, bottom))
                            End If
                            band.Current(key) = index
                        End If
                        runStart = -1
                    End If
                Next
                Dim swap = band.Previous
                band.Previous = band.Current
                band.Current = swap
            Next
                End Sub)
            For Each band In maskBands
                rectangles.AddRange(band.Rectangles)
            Next
            _TransparencyMaskFrameCount += 1
            If ProfileMask Then _MaskScanMilliseconds += Diagnostics.Stopwatch.GetElapsedTime(profileStarted).TotalMilliseconds
            Dim clientSize As New Microsoft.Xna.Framework.Point(clientWidth, clientHeight)
            If TransparencyMaskApplied AndAlso clientSize = lastMaskClientSize AndAlso
               rectangles.SequenceEqual(lastMaskRectangles) Then Return

            Dim windowRegion = IntPtr.Zero
            If ProfileMask Then profileStarted = Diagnostics.Stopwatch.GetTimestamp()
            Try
                ' Native rectangle regions avoid rebuilding a GDI+ winding path
                ' and converting it back to an HRGN on every scrolling frame.
                Dim length = 8 + rectangles.Count * 4
                If regionData.Length < length Then regionData = New Integer(Math.Max(length, regionData.Length * 2) - 1) {}
                regionData(0) = 32
                regionData(1) = 1
                regionData(2) = rectangles.Count
                regionData(3) = rectangles.Count * 16
                regionData(4) = 0 : regionData(5) = 0
                regionData(6) = clientWidth : regionData(7) = clientHeight
                For i = 0 To rectangles.Count - 1
                    Dim rect = rectangles(i)
                    regionData(8 + i * 4) = rect.Left
                    regionData(9 + i * 4) = rect.Top
                    regionData(10 + i * 4) = rect.Right
                    regionData(11 + i * 4) = rect.Bottom
                Next
                Dim pin = GCHandle.Alloc(regionData, GCHandleType.Pinned)
                Try
                    windowRegion = ExtCreateRegion(IntPtr.Zero, CUInt(length * 4), pin.AddrOfPinnedObject())
                Finally
                    pin.Free()
                End Try
                If windowRegion = IntPtr.Zero Then
                    TransparencyMaskErrorCode = Marshal.GetLastWin32Error()
                    Return
                End If

                TransparencyMaskErrorCode = 0
                ' The owner presents the complete DX frame immediately after
                ' this call. A synchronous Win32 repaint adds no pixels and
                ' needlessly blocks that presentation on every scrolling frame.
                If SetWindowRgn(owner, windowRegion, False) = 0 Then
                    TransparencyMaskErrorCode = Marshal.GetLastWin32Error()
                    Return
                End If

                ' SetWindowRgn owns the HRGN after success.
                windowRegion = IntPtr.Zero
                TransparencyMaskApplied = True
                TransparencyMaskRectangleCount = rectangles.Count
                lastMaskRectangles = rectangles.ToArray()
                lastMaskClientSize = clientSize
                _TransparencyMaskUpdateCount += 1
            Finally
                If windowRegion <> IntPtr.Zero Then DeleteObject(windowRegion)
                If ProfileMask Then _MaskApplyMilliseconds += Diagnostics.Stopwatch.GetElapsedTime(profileStarted).TotalMilliseconds
            End Try
        End Sub

        Public Sub Tick()
            If disposed OrElse synchronizing OrElse Not IsWindow(owner) Then Return
            synchronizing = True
            Try
                SynchronizeOwnerWindow()
            Finally
                synchronizing = False
            End Try
        End Sub

        Private Shared Function NextVisibleWindow(window As IntPtr) As IntPtr
            Dim following = GetWindow(window, 2UI)
            While following <> IntPtr.Zero AndAlso Not IsWindowVisible(following)
                following = GetWindow(following, 2UI)
            End While
            Return following
        End Function

        Private Sub SynchronizeOwnerWindow()
            Dim visible = IsWindowVisible(owner) AndAlso Not IsIconic(owner)
            If Not visible Then
                If lastVisible Then backdrop.Hide()
                lastVisible = False
                Return
            End If

            Dim rect As NativeRect
            If Not GetWindowRect(owner, rect) Then Return
            Dim bounds = Global.System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
            If lastVisible AndAlso backdrop.Visible AndAlso bounds = lastBounds AndAlso
               NextVisibleWindow(owner) = backdrop.Handle Then Return
            If Not backdrop.Visible Then backdrop.Show()
            ' Insert immediately below the DX12 HWND without stealing focus.
            LastPositionSucceeded = SetWindowPos(backdrop.Handle, owner, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                                                 SwpNoActivate Or SwpShowWindow)
            If LastPositionSucceeded Then
                _NativePositionUpdateCount += 1
                lastBounds = bounds
                lastVisible = True
            End If
        End Sub

        ' Windows enters a modal message loop during caption dragging/resizing.
        ' The Game update loop may not run there. Follow the committed native
        ' position synchronously instead of leaving the acrylic HWND behind.
        Private NotInheritable Class OwnerWindowObserver
            Inherits NativeWindow
            Private ReadOnly host As DwmBackdropHost

            Public Sub New(handle As IntPtr, host As DwmBackdropHost)
                Me.host = host
                AssignHandle(handle)
            End Sub

            Protected Overrides Sub WndProc(ByRef message As Message)
                Dim kind = message.Msg
                MyBase.WndProc(message)
                Select Case kind
                    Case &H47, &H18, &H5, &H232 ' WINDOWPOSCHANGED, SHOWWINDOW, SIZE, EXITSIZEMOVE
                        host.Tick()
                    Case &H82 ' NCDESTROY
                        If Not host.disposed AndAlso Not host.backdrop.IsDisposed Then host.backdrop.Hide()
                End Select
            End Sub
        End Class

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            ownerWindow?.ReleaseHandle()
            If Not backdrop.IsDisposed Then backdrop.Dispose()
        End Sub

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeRect
            Public Left As Integer
            Public Top As Integer
            Public Right As Integer
            Public Bottom As Integer
        End Structure

        <DllImport("user32.dll")>
        Private Shared Function GetWindowRect(hwnd As IntPtr, ByRef rect As NativeRect) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function GetWindow(hwnd As IntPtr, command As UInteger) As IntPtr
        End Function
        <DllImport("user32.dll")>
        Private Shared Function GetClientRect(hwnd As IntPtr, ByRef rect As NativeRect) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function IsWindowVisible(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function IsIconic(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function SetWindowPos(hwnd As IntPtr, insertAfter As IntPtr, x As Integer, y As Integer,
                                             width As Integer, height As Integer, flags As UInteger) As Boolean
        End Function
        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function SetWindowRgn(hwnd As IntPtr, region As IntPtr, redraw As Boolean) As Integer
        End Function
        <DllImport("gdi32.dll")>
        Private Shared Function DeleteObject(value As IntPtr) As Boolean
        End Function
        <DllImport("gdi32.dll", SetLastError:=True)>
        Private Shared Function ExtCreateRegion(transform As IntPtr, size As UInteger, data As IntPtr) As IntPtr
        End Function
    End Class
End Namespace
