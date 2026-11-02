Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework

Namespace [Shared].Composition
    Friend NotInheritable Class X11DesktopCapture
        Implements IDesktopCaptureSource
        Private Const X11 As String = "libX11.so.6"
        Private Const Composite As String = "libXcomposite.so.1"
        Private Const Render As String = "libXrender.so.1"
        Private ReadOnly display As IntPtr
        Private ReadOnly queryDisplay As IntPtr
        Private ReadOnly window As UIntPtr
        Private ReadOnly root As UIntPtr
        Private ReadOnly overlay As UIntPtr
        Private scratch, output As UIntPtr
        Private scratchPicture, outputPicture As UIntPtr
        Private scratchSize, outputSize As Point
        Private bytes As Byte() = Array.Empty(Of Byte)()
        Private disposed As Boolean
        Private Shared ReadOnly handlers As New System.Collections.Concurrent.ConcurrentDictionary(Of IntPtr, Boolean)
        Private Shared ReadOnly handler As ErrorHandler = AddressOf HandleError
        Private Shared priorHandler As ErrorHandler
        Private Shared ReadOnly handlerGate As New Object
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function ErrorHandler(connection As IntPtr, eventData As IntPtr) As Integer
        Private Shared Function HandleError(connection As IntPtr, eventData As IntPtr) As Integer
            If handlers.ContainsKey(connection) Then
                handlers(connection) = True
                Return 0
            End If
            Return If(priorHandler Is Nothing, 0, priorHandler(connection, eventData))
        End Function
        Private Shared Sub Track(connection As IntPtr)
            SyncLock handlerGate
                If handlers.IsEmpty Then
                    Dim old = XSetErrorHandler(handler)
                    If old <> IntPtr.Zero AndAlso old <> Marshal.GetFunctionPointerForDelegate(handler) Then priorHandler = Marshal.GetDelegateForFunctionPointer(Of ErrorHandler)(old)
                End If
                handlers(connection) = False
            End SyncLock
        End Sub
        Friend Sub New(nativeWindow As IntPtr)
            Try
                XInitThreads()
                display = XOpenDisplay(IntPtr.Zero)
                queryDisplay = XOpenDisplay(IntPtr.Zero)
                If display = IntPtr.Zero OrElse queryDisplay = IntPtr.Zero Then Throw New DesktopCaptureUnavailableException(DesktopBackdropState.SessionChangeRequired, "An X11 desktop session is required for desktop blur.")
                Track(display) : Track(queryDisplay)
                window = New UIntPtr(CULng(nativeWindow.ToInt64()))
                root = XDefaultRootWindow(display)
                Dim eventBase, errorBase As Integer
                If XCompositeQueryExtension(display, eventBase, errorBase) = 0 Then Throw New DesktopCaptureUnavailableException(DesktopBackdropState.DependenciesRequired, "The X server does not provide XComposite.")
                If XRenderFindStandardFormat(display, 0) = IntPtr.Zero Then Throw New DesktopCaptureUnavailableException(DesktopBackdropState.DependenciesRequired, "The X server does not provide XRender ARGB32 support.")
                Dim selection = XInternAtom(display, "_NET_WM_CM_S" & XDefaultScreen(display), False)
                If XGetSelectionOwner(display, selection) = UIntPtr.Zero Then XCompositeRedirectSubwindows(display, root, 0)
                overlay = XCompositeGetOverlayWindow(display, root)
                XSync(display, False)
                If RootAncestor(display, window) = UIntPtr.Zero Then Throw New DesktopCaptureUnavailableException(DesktopBackdropState.Unsupported, "The host window is not in the X11 desktop tree.")
            Catch ex As DllNotFoundException
                Dispose()
                Throw New DesktopCaptureUnavailableException(DesktopBackdropState.DependenciesRequired, "Install the X11, XComposite and XRender runtime libraries to enable desktop blur.")
            Catch
                Dispose()
                Throw
            End Try
        End Sub
        Public ReadOnly Property ExcludesHostWindow As Boolean Implements IDesktopCaptureSource.ExcludesHostWindow
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property DpiScale As Single Implements IDesktopCaptureSource.DpiScale
            Get
                Return 1
            End Get
        End Property
        Public ReadOnly Property WindowBounds As Rectangle Implements IDesktopCaptureSource.WindowBounds
            Get
                Dim attributes As WindowAttributes
                Dim x, y As Integer, child As UIntPtr
                If XGetWindowAttributes(queryDisplay, window, attributes) = 0 OrElse attributes.MapState <> 2 OrElse
                    XTranslateCoordinates(queryDisplay, window, root, 0, 0, x, y, child) = 0 Then Return Rectangle.Empty
                Return New Rectangle(x, y, attributes.Width, attributes.Height)
            End Get
        End Property
        Private Shared Function RootAncestor(connection As IntPtr, child As UIntPtr) As UIntPtr
            For level = 0 To 63
                Dim rootWindow, parent As UIntPtr, children As IntPtr, count As UInteger
                If XQueryTree(connection, child, rootWindow, parent, children, count) = 0 Then Return UIntPtr.Zero
                If children <> IntPtr.Zero Then XFree(children)
                If parent = rootWindow Then Return child
                If parent = UIntPtr.Zero OrElse parent = child Then Return UIntPtr.Zero
                child = parent
            Next
            Return UIntPtr.Zero
        End Function
        Public Function TryCapture(area As Rectangle, downsample As Integer, rgba As Byte()) As Boolean Implements IDesktopCaptureSource.TryCapture
            Dim ownerFrame = RootAncestor(display, window)
            If ownerFrame = UIntPtr.Zero Then Return False
            Dim size As New Point(area.Width, area.Height)
            Dim reduced As New Point((area.Width + downsample - 1) \ downsample, (area.Height + downsample - 1) \ downsample)
            If scratch = UIntPtr.Zero OrElse size <> scratchSize OrElse reduced <> outputSize Then
                FreeTargets()
                Dim format = XRenderFindStandardFormat(display, 0) ' ARGB32
                scratch = XCreatePixmap(display, root, CUInt(size.X), CUInt(size.Y), 32)
                output = XCreatePixmap(display, root, CUInt(reduced.X), CUInt(reduced.Y), 32)
                scratchPicture = XRenderCreatePicture(display, scratch, format, UIntPtr.Zero, IntPtr.Zero)
                outputPicture = XRenderCreatePicture(display, output, format, UIntPtr.Zero, IntPtr.Zero)
                scratchSize = size : outputSize = reduced
            End If
            Dim background As New RenderColor With {.Red = 24US * 257US, .Green = 28US * 257US, .Blue = 32US * 257US, .Alpha = UShort.MaxValue}
            XRenderFillRectangle(display, 1, scratchPicture, background, 0, 0, CUInt(size.X), CUInt(size.Y))
            ' The root's wallpaper pixmap is safe; sampling the composited root is not.
            For Each propertyName In {"_XROOTPMAP_ID", "ESETROOT_PMAP_ID"}
                Dim actualType As UIntPtr, count, remaining As IntPtr, data As IntPtr
                Dim actualFormat As Integer
                If XGetWindowProperty(display, root, XInternAtom(display, propertyName, True), IntPtr.Zero, New IntPtr(1), False, New UIntPtr(20UI), actualType, actualFormat, count, remaining, data) = 0 AndAlso data <> IntPtr.Zero Then
                    Try
                        If actualFormat = 32 AndAlso count.ToInt64() > 0 Then
                            Dim pixmap = New UIntPtr(CULng(Marshal.ReadIntPtr(data).ToInt64()))
                            Dim picture = XRenderCreatePicture(display, pixmap, XRenderFindVisualFormat(display, XDefaultVisual(display, XDefaultScreen(display))), UIntPtr.Zero, IntPtr.Zero)
                            XRenderComposite(display, 1, picture, UIntPtr.Zero, scratchPicture, area.X, area.Y, 0, 0, 0, 0, CUInt(size.X), CUInt(size.Y))
                            XRenderFreePicture(display, picture)
                            Exit For
                        End If
                    Finally
                        XFree(data)
                    End Try
                End If
            Next
            Dim rootWindow, parent As UIntPtr, children As IntPtr, childCount As UInteger
            If XQueryTree(display, root, rootWindow, parent, children, childCount) = 0 Then Return False
            Try
                For index = 0 To Math.Min(CInt(childCount), 4096) - 1
                    Dim child = New UIntPtr(CULng(Marshal.ReadIntPtr(children, index * IntPtr.Size).ToInt64()))
                    If child = ownerFrame OrElse child = overlay Then Continue For
                    Dim attributes As WindowAttributes
                    If XGetWindowAttributes(display, child, attributes) = 0 OrElse attributes.MapState <> 2 OrElse attributes.ClassCode <> 1 Then Continue For
                    Dim bounds As New Rectangle(attributes.X, attributes.Y, attributes.Width, attributes.Height)
                    If Not bounds.Intersects(area) Then Continue For
                    handlers(display) = False
                    Dim pixmap = XCompositeNameWindowPixmap(display, child)
                    XSync(display, False)
                    If pixmap = UIntPtr.Zero OrElse handlers(display) Then Continue For
                    Dim picture = XRenderCreatePicture(display, pixmap, XRenderFindVisualFormat(display, attributes.Visual), UIntPtr.Zero, IntPtr.Zero)
                    XRenderComposite(display, 3, picture, UIntPtr.Zero, scratchPicture, 0, 0, 0, 0, bounds.X - area.X, bounds.Y - area.Y, CUInt(bounds.Width), CUInt(bounds.Height))
                    XRenderFreePicture(display, picture)
                    XFreePixmap(display, pixmap)
                Next
            Finally
                If children <> IntPtr.Zero Then XFree(children)
            End Try
            Dim transform As New RenderTransform With {.M11 = downsample * 65536, .M22 = downsample * 65536, .M33 = 65536}
            XRenderSetPictureTransform(display, scratchPicture, transform)
            XRenderSetPictureFilter(display, scratchPicture, "good", IntPtr.Zero, 0)
            XRenderComposite(display, 1, scratchPicture, UIntPtr.Zero, outputPicture, 0, 0, 0, 0, 0, 0, CUInt(reduced.X), CUInt(reduced.Y))
            transform.M11 = 65536 : transform.M22 = 65536
            XRenderSetPictureTransform(display, scratchPicture, transform)
            Dim image = XGetImage(display, output, 0, 0, CUInt(reduced.X), CUInt(reduced.Y), UIntPtr.MaxValue, 2)
            If image = IntPtr.Zero Then Return False
            Try
                Dim rowBytes = Marshal.ReadInt32(image, If(IntPtr.Size = 8, 44, 40))
                Dim bits = Marshal.ReadInt32(image, If(IntPtr.Size = 8, 48, 44))
                If bits <> 32 Then Throw New NotSupportedException("Desktop blur requires a 32-bit XRender image.")
                Dim pointer = Marshal.ReadIntPtr(image, 16)
                Dim required = rowBytes * reduced.Y
                If bytes.Length <> required Then ReDim bytes(required - 1)
                Marshal.Copy(pointer, bytes, 0, required)
                For y = 0 To reduced.Y - 1
                    For x = 0 To reduced.X - 1
                        Dim source = y * rowBytes + x * 4, target = (y * reduced.X + x) * 4
                        rgba(target) = bytes(source + 2) : rgba(target + 1) = bytes(source + 1) : rgba(target + 2) = bytes(source) : rgba(target + 3) = 255
                    Next
                Next
                Return True
            Finally
                XDestroyImage(image)
            End Try
        End Function
        Private Sub FreeTargets()
            If scratchPicture <> UIntPtr.Zero Then XRenderFreePicture(display, scratchPicture)
            If outputPicture <> UIntPtr.Zero Then XRenderFreePicture(display, outputPicture)
            If scratch <> UIntPtr.Zero Then XFreePixmap(display, scratch)
            If output <> UIntPtr.Zero Then XFreePixmap(display, output)
            scratch = UIntPtr.Zero : output = UIntPtr.Zero : scratchPicture = UIntPtr.Zero : outputPicture = UIntPtr.Zero
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            If display <> IntPtr.Zero Then
                FreeTargets()
                If overlay <> UIntPtr.Zero Then XCompositeReleaseOverlayWindow(display, root)
                XCloseDisplay(display)
                Dim ignored As Boolean
                handlers.TryRemove(display, ignored)
            End If
            If queryDisplay <> IntPtr.Zero Then
                XCloseDisplay(queryDisplay)
                Dim ignored As Boolean
                handlers.TryRemove(queryDisplay, ignored)
            End If
        End Sub
        <StructLayout(LayoutKind.Sequential)>
        Private Structure WindowAttributes
            Public X, Y, Width, Height, BorderWidth, Depth As Integer
            Public Visual As IntPtr
            Public Root As UIntPtr
            Public ClassCode, BitGravity, WinGravity, BackingStore As Integer
            Public BackingPlanes, BackingPixel As UIntPtr
            Public SaveUnder As Integer
            Public Colormap As UIntPtr
            Public MapInstalled, MapState As Integer
            Public AllEvents, YourEvents, DoNotPropagate As IntPtr
            Public OverrideRedirect As Integer
            Public Screen As IntPtr
        End Structure
        <StructLayout(LayoutKind.Sequential)>
        Private Structure RenderColor
            Public Red, Green, Blue, Alpha As UShort
        End Structure
        <StructLayout(LayoutKind.Sequential)>
        Private Structure RenderTransform
            Public M11, M12, M13, M21, M22, M23, M31, M32, M33 As Integer
        End Structure
        <DllImport(X11)> Private Shared Function XInitThreads() As Integer
        End Function
        <DllImport(X11)> Private Shared Function XOpenDisplay(name As IntPtr) As IntPtr
        End Function
        <DllImport(X11)> Private Shared Function XCloseDisplay(d As IntPtr) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XDefaultRootWindow(d As IntPtr) As UIntPtr
        End Function
        <DllImport(X11)> Private Shared Function XDefaultScreen(d As IntPtr) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XDefaultVisual(d As IntPtr, screen As Integer) As IntPtr
        End Function
        <DllImport(X11)> Private Shared Function XGetWindowAttributes(d As IntPtr, w As UIntPtr, ByRef a As WindowAttributes) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XTranslateCoordinates(d As IntPtr, w As UIntPtr, root As UIntPtr, sx As Integer, sy As Integer, ByRef x As Integer, ByRef y As Integer, ByRef child As UIntPtr) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XQueryTree(d As IntPtr, w As UIntPtr, ByRef root As UIntPtr, ByRef parent As UIntPtr, ByRef children As IntPtr, ByRef count As UInteger) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XFree(data As IntPtr) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XSync(d As IntPtr, discard As Boolean) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XSetErrorHandler(handler As ErrorHandler) As IntPtr
        End Function
        <DllImport(X11, CharSet:=CharSet.Ansi)> Private Shared Function XInternAtom(d As IntPtr, name As String, onlyExisting As Boolean) As UIntPtr
        End Function
        <DllImport(X11)> Private Shared Function XGetSelectionOwner(d As IntPtr, atom As UIntPtr) As UIntPtr
        End Function
        <DllImport(X11)> Private Shared Function XGetWindowProperty(d As IntPtr, w As UIntPtr, atom As UIntPtr, offset As IntPtr, length As IntPtr, delete As Boolean, requested As UIntPtr, ByRef actual As UIntPtr, ByRef format As Integer, ByRef count As IntPtr, ByRef remaining As IntPtr, ByRef data As IntPtr) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XCreatePixmap(d As IntPtr, drawable As UIntPtr, width As UInteger, height As UInteger, depth As UInteger) As UIntPtr
        End Function
        <DllImport(X11)> Private Shared Function XFreePixmap(d As IntPtr, pixmap As UIntPtr) As Integer
        End Function
        <DllImport(X11)> Private Shared Function XGetImage(d As IntPtr, drawable As UIntPtr, x As Integer, y As Integer, width As UInteger, height As UInteger, mask As UIntPtr, format As Integer) As IntPtr
        End Function
        <DllImport(X11)> Private Shared Function XDestroyImage(image As IntPtr) As Integer
        End Function
        <DllImport(Composite)> Private Shared Function XCompositeQueryExtension(d As IntPtr, ByRef eventBase As Integer, ByRef errorBase As Integer) As Integer
        End Function
        <DllImport(Composite)> Private Shared Sub XCompositeRedirectSubwindows(d As IntPtr, root As UIntPtr, mode As Integer)
        End Sub
        <DllImport(Composite)> Private Shared Function XCompositeNameWindowPixmap(d As IntPtr, w As UIntPtr) As UIntPtr
        End Function
        <DllImport(Composite)> Private Shared Function XCompositeGetOverlayWindow(d As IntPtr, root As UIntPtr) As UIntPtr
        End Function
        <DllImport(Composite)> Private Shared Sub XCompositeReleaseOverlayWindow(d As IntPtr, root As UIntPtr)
        End Sub
        <DllImport(Render)> Private Shared Function XRenderFindStandardFormat(d As IntPtr, format As Integer) As IntPtr
        End Function
        <DllImport(Render)> Private Shared Function XRenderFindVisualFormat(d As IntPtr, visual As IntPtr) As IntPtr
        End Function
        <DllImport(Render)> Private Shared Function XRenderCreatePicture(d As IntPtr, drawable As UIntPtr, format As IntPtr, mask As UIntPtr, attributes As IntPtr) As UIntPtr
        End Function
        <DllImport(Render)> Private Shared Sub XRenderFreePicture(d As IntPtr, picture As UIntPtr)
        End Sub
        <DllImport(Render)> Private Shared Sub XRenderFillRectangle(d As IntPtr, op As Integer, picture As UIntPtr, ByRef color As RenderColor, x As Integer, y As Integer, width As UInteger, height As UInteger)
        End Sub
        <DllImport(Render)> Private Shared Sub XRenderComposite(d As IntPtr, op As Integer, source As UIntPtr, mask As UIntPtr, dest As UIntPtr, sx As Integer, sy As Integer, mx As Integer, my As Integer, dx As Integer, dy As Integer, width As UInteger, height As UInteger)
        End Sub
        <DllImport(Render)> Private Shared Sub XRenderSetPictureTransform(d As IntPtr, picture As UIntPtr, ByRef transform As RenderTransform)
        End Sub
        <DllImport(Render, CharSet:=CharSet.Ansi)> Private Shared Sub XRenderSetPictureFilter(d As IntPtr, picture As UIntPtr, filter As String, parameters As IntPtr, count As Integer)
        End Sub
    End Class
End Namespace
