Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Text
Imports Microsoft.Xna.Framework

Namespace Windows.Composition
    Public Enum WindowBackdrop
        None = 1
        Mica = 2
        Acrylic = 3
        Tabbed = 4
    End Enum

    Public Class WindowCompositionOptions
        Public Property Borderless As Boolean = True
        Public Property Resizable As Boolean = True
        Public Property DarkMode As Boolean = True
        Public Property ExtendFrameIntoClientArea As Boolean = True
        Public Property Backdrop As WindowBackdrop = WindowBackdrop.Acrylic
        Public Property FallbackAccentBlur As Boolean = True
        Public Property Tint As Color = New Color(2, 8, 6, 92)
        Public Property Opacity As Byte = Byte.MaxValue
        ''' <summary>
        ''' Optional color-key fallback for hosts whose presentation path honors
        ''' layered-window color keys. DXGI flip-model swap chains can report that
        ''' this succeeded while still presenting the key color as opaque.
        ''' </summary>
        Public Property UseColorKeyTransparency As Boolean
        Public Property TransparentColorKey As Color = New Color(1, 0, 1)
    End Class

    Public Class WindowCompositionResult
        Public Property Handle As IntPtr
        Public Property CompositionEnabled As Boolean
        Public Property FrameExtended As Boolean
        Public Property BackdropEnabled As Boolean
        Public Property AccentBlurEnabled As Boolean
        Public Property BlurBehindEnabled As Boolean
        Public Property LayeredStyleEnabled As Boolean
        Public Property LayeredWindowEnabled As Boolean
        Public Property LayeredWindowErrorCode As Integer
        Public Property ErrorMessage As String = String.Empty

        Public ReadOnly Property Succeeded As Boolean
            Get
                Return Handle <> IntPtr.Zero AndAlso String.IsNullOrEmpty(ErrorMessage)
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Win32/DWM composition support for Project-Z Game windows. MonoGame's
    ''' native DX12 backend exposes an SDL pointer through GameWindow.Handle, so
    ''' this class also resolves the real HWND by its unique window title.
    ''' </summary>
    Public NotInheritable Class WindowsComposition
        Private Const GwlStyle = -16
        Private Const GwlExStyle = -20
        Private Const WsCaption As Long = &HC00000L
        Private Const WsThickFrame As Long = &H40000L
        Private Const WsMinimizeBox As Long = &H20000L
        Private Const WsMaximizeBox As Long = &H10000L
        Private Const WsSysMenu As Long = &H80000L
        Private Const WsExLayered As Long = &H80000L
        Private Const LwaColorKey As UInteger = 1UI
        Private Const LwaAlpha As UInteger = 2UI
        Private Const SwpNoMove As UInteger = 2UI
        Private Const SwpNoSize As UInteger = 1UI
        Private Const SwpNoZOrder As UInteger = 4UI
        Private Const SwpNoActivate As UInteger = &H10UI
        Private Const SwpFrameChanged As UInteger = &H20UI
        Private Const DwmwaUseImmersiveDarkMode = 20
        Private Const DwmwaWindowCornerPreference = 33
        Private Const DwmwaBorderColor = 34
        Private Const DwmwaSystemBackdropType = 38
        Private Const DwmColorNone = -2

        Private Sub New()
        End Sub

        Public Shared Function Apply(game As Game, options As WindowCompositionOptions) As WindowCompositionResult
            Return ApplyToHandle(ResolveHwnd(game), options)
        End Function

        Public Shared Function ApplyToHandle(hwnd As IntPtr, options As WindowCompositionOptions) As WindowCompositionResult
            Dim result As New WindowCompositionResult()
            Try
                result.Handle = hwnd
                If result.Handle = IntPtr.Zero Then Throw New InvalidOperationException("Project-Z could not resolve its native window handle.")

                Dim style = GetWindowLongPtr(result.Handle, GwlStyle).ToInt64()
                If options.Borderless Then
                    style = style And Not (WsCaption Or WsSysMenu)
                    If options.Resizable Then
                        style = style Or WsThickFrame Or WsMinimizeBox Or WsMaximizeBox
                    Else
                        style = style And Not (WsThickFrame Or WsMinimizeBox Or WsMaximizeBox)
                    End If
                    SetWindowLongPtr(result.Handle, GwlStyle, New IntPtr(style))
                    SetWindowPos(result.Handle, IntPtr.Zero, 0, 0, 0, 0,
                                 SwpNoMove Or SwpNoSize Or SwpNoZOrder Or SwpNoActivate Or SwpFrameChanged)
                End If

                Dim composition As Boolean
                result.CompositionEnabled = DwmIsCompositionEnabled(composition) = 0 AndAlso composition
                If result.CompositionEnabled Then
                    Dim dark = If(options.DarkMode, 1, 0)
                    DwmSetWindowAttribute(result.Handle, DwmwaUseImmersiveDarkMode, dark, Marshal.SizeOf(Of Integer)())
                    ' A region-masked borderless window can contain many disjoint
                    ' visible islands. Asking DWM to round and border that region
                    ' makes card edges appear as large circles/ovals. The scene
                    ' draws its own chrome, so suppress native border treatment.
                    Dim corners = If(options.Borderless, 1, 2) ' DO_NOT_ROUND / ROUND
                    DwmSetWindowAttribute(result.Handle, DwmwaWindowCornerPreference, corners, Marshal.SizeOf(Of Integer)())
                    If options.Borderless Then
                        Dim noBorder = DwmColorNone
                        DwmSetWindowAttribute(result.Handle, DwmwaBorderColor, noBorder, Marshal.SizeOf(Of Integer)())
                    End If

                    If options.ExtendFrameIntoClientArea Then
                        Dim margins As New Margins With {.Left = -1, .Right = -1, .Top = -1, .Bottom = -1}
                        result.FrameExtended = DwmExtendFrameIntoClientArea(result.Handle, margins) = 0
                    End If

                    Dim blur As New DwmBlurBehind With {.Flags = 1UI, .Enable = True}
                    result.BlurBehindEnabled = DwmEnableBlurBehindWindow(result.Handle, blur) = 0

                    Dim backdrop = CInt(options.Backdrop)
                    result.BackdropEnabled = options.Backdrop <> WindowBackdrop.None AndAlso
                                             DwmSetWindowAttribute(result.Handle, DwmwaSystemBackdropType, backdrop,
                                                                   Marshal.SizeOf(Of Integer)()) = 0

                    ' DWM can accept DWMWA_SYSTEMBACKDROP_TYPE for a borderless
                    ' flip-model window without actually painting a blurred client
                    ' backdrop. Apply the accent policy as the visual fallback even
                    ' when that attribute returned S_OK.
                    If options.FallbackAccentBlur Then
                        result.AccentBlurEnabled = ApplyAccentBlur(result.Handle, options.Tint)
                    End If
                End If

                If options.Opacity < Byte.MaxValue OrElse options.UseColorKeyTransparency Then
                    Dim exStyle = GetWindowLongPtr(result.Handle, GwlExStyle).ToInt64() Or WsExLayered
                    SetWindowLongPtr(result.Handle, GwlExStyle, New IntPtr(exStyle))
                    SetWindowPos(result.Handle, IntPtr.Zero, 0, 0, 0, 0,
                                 SwpNoMove Or SwpNoSize Or SwpNoZOrder Or SwpNoActivate Or SwpFrameChanged)
                    result.LayeredStyleEnabled = (GetWindowLongPtr(result.Handle, GwlExStyle).ToInt64() And WsExLayered) <> 0
                    Dim flags = If(options.UseColorKeyTransparency, LwaColorKey, 0UI) Or
                                If(options.Opacity < Byte.MaxValue, LwaAlpha, 0UI)
                    Dim colorKey = ToColorRef(options.TransparentColorKey)
                    result.LayeredWindowEnabled = SetLayeredWindowAttributes(result.Handle, colorKey, options.Opacity, flags)
                    If Not result.LayeredWindowEnabled Then result.LayeredWindowErrorCode = Marshal.GetLastWin32Error()
                Else
                    ' WS_EX_LAYERED prevents acrylic/system backdrops on current
                    ' Windows builds. Remove a stale style when the caller uses
                    ' DWM composition or a native window-region mask instead.
                    Dim exStyle = GetWindowLongPtr(result.Handle, GwlExStyle).ToInt64() And Not WsExLayered
                    SetWindowLongPtr(result.Handle, GwlExStyle, New IntPtr(exStyle))
                End If

                RedrawWindow(result.Handle, IntPtr.Zero, IntPtr.Zero, &H585UI)
            Catch ex As Exception
                result.ErrorMessage = ex.Message
            End Try
            Return result
        End Function

        Public Shared Function ResolveHwnd(game As Game) As IntPtr
            Dim candidate = game.Window.Handle
            If candidate <> IntPtr.Zero AndAlso IsWindow(candidate) Then Return candidate

            Dim title = game.Window.Title
            Dim resolved = IntPtr.Zero
            Dim currentPid = CUInt(Environment.ProcessId)
            Dim callback As EnumWindowsProc =
                Function(window, parameter)
                    Dim pid As UInteger
                    GetWindowThreadProcessId(window, pid)
                    If pid <> currentPid Then Return True
                    Dim length = GetWindowTextLength(window)
                    If length <= 0 Then Return True
                    Dim text As New StringBuilder(length + 1)
                    GetWindowText(window, text, text.Capacity)
                    If String.Equals(text.ToString(), title, StringComparison.Ordinal) Then
                        resolved = window
                        Return False
                    End If
                    Return True
                End Function
            EnumThreadWindows(GetCurrentThreadId(), callback, IntPtr.Zero)
            If resolved = IntPtr.Zero Then EnumWindows(callback, IntPtr.Zero)
            Return resolved
        End Function

        Private Shared Function ApplyAccentBlur(hwnd As IntPtr, tint As Color) As Boolean
            Dim gradientBits = CUInt(tint.A) << 24 Or CUInt(tint.B) << 16 Or CUInt(tint.G) << 8 Or tint.R
            ' Preserve the high alpha bit as its signed two's-complement value.
            ' CInt(UInt32) is checked in VB and overflows for alpha >= 128.
            Dim gradient = BitConverter.ToInt32(BitConverter.GetBytes(gradientBits), 0)
            Dim accent As New AccentPolicy With {
                .AccentState = 4, ' ACCENT_ENABLE_ACRYLICBLURBEHIND
                .AccentFlags = 2,
                .GradientColor = gradient
            }
            Dim size = Marshal.SizeOf(Of AccentPolicy)()
            Dim pointer = Marshal.AllocHGlobal(size)
            Try
                Marshal.StructureToPtr(accent, pointer, False)
                Dim data As New WindowCompositionAttributeData With {
                    .Attribute = 19, ' WCA_ACCENT_POLICY
                    .Data = pointer,
                    .SizeOfData = size
                }
                Return SetWindowCompositionAttribute(hwnd, data) <> 0
            Finally
                Marshal.FreeHGlobal(pointer)
            End Try
        End Function

        Private Shared Function ToColorRef(color As Color) As UInteger
            Return CUInt(color.R) Or (CUInt(color.G) << 8) Or (CUInt(color.B) << 16)
        End Function

        <StructLayout(LayoutKind.Sequential)>
    Private Structure Margins
            Public Left As Integer
            Public Right As Integer
            Public Top As Integer
            Public Bottom As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure DwmBlurBehind
        Public Flags As UInteger
        <MarshalAs(UnmanagedType.Bool)>
        Public Enable As Boolean
        Public BlurRegion As IntPtr
        <MarshalAs(UnmanagedType.Bool)>
        Public TransitionOnMaximized As Boolean
    End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure AccentPolicy
            Public AccentState As Integer
            Public AccentFlags As Integer
            Public GradientColor As Integer
            Public AnimationId As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure WindowCompositionAttributeData
            Public Attribute As Integer
            Public Data As IntPtr
            Public SizeOfData As Integer
        End Structure

        Private Delegate Function EnumWindowsProc(hwnd As IntPtr, parameter As IntPtr) As Boolean

        <DllImport("dwmapi.dll")>
        Private Shared Function DwmIsCompositionEnabled(ByRef enabled As Boolean) As Integer
        End Function
        <DllImport("dwmapi.dll")>
        Private Shared Function DwmExtendFrameIntoClientArea(hwnd As IntPtr, ByRef margins As Margins) As Integer
        End Function
        <DllImport("dwmapi.dll")>
        Private Shared Function DwmEnableBlurBehindWindow(hwnd As IntPtr, ByRef blur As DwmBlurBehind) As Integer
        End Function
        <DllImport("dwmapi.dll")>
        Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
        End Function
        <DllImport("user32.dll")>
        Private Shared Function SetWindowCompositionAttribute(hwnd As IntPtr, ByRef data As WindowCompositionAttributeData) As Integer
        End Function
        <DllImport("user32.dll", EntryPoint:="GetWindowLongPtrW", SetLastError:=True)>
        Private Shared Function GetWindowLongPtr(hwnd As IntPtr, index As Integer) As IntPtr
        End Function
        <DllImport("user32.dll", EntryPoint:="SetWindowLongPtrW", SetLastError:=True)>
        Private Shared Function SetWindowLongPtr(hwnd As IntPtr, index As Integer, value As IntPtr) As IntPtr
        End Function
        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function SetWindowPos(hwnd As IntPtr, after As IntPtr, x As Integer, y As Integer,
                                             width As Integer, height As Integer, flags As UInteger) As Boolean
        End Function
        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function SetLayeredWindowAttributes(hwnd As IntPtr, colorKey As UInteger, alpha As Byte,
                                                           flags As UInteger) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function RedrawWindow(hwnd As IntPtr, updateRect As IntPtr, region As IntPtr, flags As UInteger) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function IsWindow(hwnd As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function EnumWindows(callback As EnumWindowsProc, parameter As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function EnumThreadWindows(threadId As UInteger, callback As EnumWindowsProc, parameter As IntPtr) As Boolean
        End Function
        <DllImport("kernel32.dll")>
        Private Shared Function GetCurrentThreadId() As UInteger
        End Function
        <DllImport("user32.dll")>
        Private Shared Function GetWindowThreadProcessId(hwnd As IntPtr, ByRef processId As UInteger) As UInteger
        End Function
        <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function GetWindowTextLength(hwnd As IntPtr) As Integer
        End Function
        <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function GetWindowText(hwnd As IntPtr, text As StringBuilder, maxCount As Integer) As Integer
        End Function
    End Class
End Namespace
