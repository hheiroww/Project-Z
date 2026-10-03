Imports System.Collections.Generic
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Content

#If WINDOWS Then
Imports ProjectZ.Windows.Input
Imports System.Runtime.InteropServices
#End If

Namespace [Shared].Drawing

    Public Class SceneManager
        Implements IDisposable

#Region "Properties"

#If WINDOWS Then
        Public Property UseHardwareInput As Boolean
            Get
                Return _UseHardwareInput
            End Get
            Set(value As Boolean)
                _UseHardwareInput = value
                ' Hardware mode polls the foreground HWND. A global WH_MOUSE_LL
                ' hook stalls the desktop when Project-Z is inactive/minimized
                ' because it receives every system mouse packet.
                If MouseHook IsNot Nothing Then
                    MouseHook.Uninstall()
                    MouseHook = Nothing
                End If
            End Set
        End Property
        Private _UseHardwareInput As Boolean = False
#End If

        Public Property LimitFPS As Integer
            Get
                Return _LimitFPS
            End Get
            Set(value As Integer)
                _LimitFPS = Math.Max(Math.Min(value, 1000), 1)
                SetLimit = True
            End Set
        End Property
        Private _LimitFPS As Integer = 60
        Private SetLimit As Boolean = False
        Private Property isDragging As Boolean = False
        Private Property Scenes As New Dictionary(Of String, Scene)
        Public Property ActiveScene As Scene
        Public Property Sender As Game
        ''' <summary>
        ''' Uses input injected by an embedding host's real child HWND instead
        ''' of MonoGame's process-global mouse and keyboard state.
        ''' </summary>
        Public Property UseExternalInput As Boolean

        ''' <summary>
        ''' Logical render size used by an off-screen host. When unset, external
        ''' input continues to map to the Game back buffer as before.
        ''' </summary>
        Public Property ExternalViewportSize As Point

        ''' <summary>Keeps the DX backbuffer/viewport matched to the native client area.</summary>
        Public Property AutoResizeViewport As Boolean = True
        Public Property MinimumViewportSize As New Point(320, 200)

        Public ReadOnly Property ViewportScale As Vector2
            Get
                Dim bounds = Sender.Window.ClientBounds
                If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Vector2.One
                Return New Vector2(
                    Sender.GraphicsDevice.PresentationParameters.BackBufferWidth / CSng(bounds.Width),
                    Sender.GraphicsDevice.PresentationParameters.BackBufferHeight / CSng(bounds.Height))
            End Get
        End Property

        Public Event ViewportResized(oldSize As Point, newSize As Point)
        Private graphicsManager As GraphicsDeviceManager
        Private resizeInProgress As Boolean

#End Region

#Region "Input"

#Region "Keypress"

        Private LastKeyboardState As KeyboardState = Keyboard.GetState
        Private LastKeyHoldTick As Long = 0
        Private LastKeyProcessTick As Long = 0
        Private Const OneSecond As Long = 10000000

        Private DoDuplicate As Boolean = False
        Private DuplicateKey As Keys
        Private ReadOnly ExternalKeys As New HashSet(Of Keys)()

        Public ReadOnly Property UsesNativeTextInput As Boolean
            Get
#If WINDOWS Then
                Return nativeKeyboard IsNot Nothing AndAlso Not UseExternalInput
#Else
                Return False
#End If
            End Get
        End Property

#If WINDOWS Then
        Private nativeKeyboard As KeyboardMessageWindow

        ' Preserve Windows message order, including taps entirely between frames.
        ' Never run scene callbacks reentrantly inside the window procedure.
        Private Class KeyboardMessageWindow
            Inherits System.Windows.Forms.NativeWindow
            Public ReadOnly Messages As New System.Collections.Concurrent.ConcurrentQueue(Of (Id As Integer, Value As Integer, Flags As Long))
            Public Sub New(handle As IntPtr)
                AssignHandle(handle)
            End Sub
            Protected Overrides Sub WndProc(ByRef m As System.Windows.Forms.Message)
                Select Case m.Msg
                    Case &H100, &H101, &H102, &H104, &H105, &H8
                        Messages.Enqueue((m.Msg, CInt(m.WParam.ToInt64()), m.LParam.ToInt64()))
                End Select
                MyBase.WndProc(m)
            End Sub
        End Class

        Private Sub DrainKeyboardMessages()
            If nativeKeyboard Is Nothing Then
                Dim hwnd = Global.ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(Sender)
                If hwnd = IntPtr.Zero Then Return
                nativeKeyboard = New KeyboardMessageWindow(hwnd)
            End If
            ActiveScene.UsesNativeTextInput = True
            Dim entry As (Id As Integer, Value As Integer, Flags As Long)
            While nativeKeyboard.Messages.TryDequeue(entry)
                If entry.Id = &H8 Then
                    ExternalKeys.Clear()
                    Continue While
                End If
                If entry.Id = &H102 Then
                    Dim character = ChrW(entry.Value And &HFFFF)
                    If Not Char.IsControl(character) Then ActiveScene.InsertNativeText(character.ToString())
                    Continue While
                End If
                Dim key = CType(entry.Value, Keys)
                If entry.Value = &H10 Then key = If(((entry.Flags >> 16) And &HFF) = &H36, Keys.RightShift, Keys.LeftShift)
                If entry.Value = &H11 Then key = If((entry.Flags And &H1000000) <> 0, Keys.RightControl, Keys.LeftControl)
                If entry.Value = &H12 Then key = If((entry.Flags And &H1000000) <> 0, Keys.RightAlt, Keys.LeftAlt)
                If entry.Id = &H100 OrElse entry.Id = &H104 Then
                    If ExternalKeys.Add(key) Then ActiveScene.KeyDown(key, New KeyboardState(ExternalKeys.ToArray()))
                    ProcessKeyPress(key, New KeyboardState(ExternalKeys.ToArray()))
                Else
                    ExternalKeys.Remove(key)
                    ActiveScene.KeyUp(key, New KeyboardState(ExternalKeys.ToArray()))
                End If
            End While
        End Sub
#End If

        Private Sub DetectKeyPress(gameTime As GameTime)
            Dim KeyboardState As KeyboardState = Keyboard.GetState
            Dim currentHoldTick As Long = gameTime.TotalGameTime.Ticks

            For i As Integer = 0 To 254
                Dim Key As Keys = CType(i, Keys)
                If KeyboardState.IsKeyDown(Key) And LastKeyboardState.IsKeyUp(Key) Then
                    LastKeyHoldTick = gameTime.TotalGameTime.Ticks
                    ActiveScene.KeyDown(Key, KeyboardState)
                    ProcessKeyPress(Key, KeyboardState)
                    If Key <> Keys.LeftShift AndAlso Key <> Keys.RightShift AndAlso
                       Key <> Keys.LeftControl AndAlso Key <> Keys.RightControl AndAlso
                       Key <> Keys.LeftAlt AndAlso Key <> Keys.RightAlt Then
                        DuplicateKey = Key
                        LastKeyProcessTick = currentHoldTick + 3000000
                        DoDuplicate = True
                    End If
                ElseIf KeyboardState.IsKeyUp(Key) And LastKeyboardState.IsKeyDown(Key) Then
                    LastKeyHoldTick = gameTime.TotalGameTime.Ticks
                    ActiveScene.KeyUp(Key, KeyboardState)
                    If Key = DuplicateKey Then DoDuplicate = False
                End If
            Next

            If DoDuplicate AndAlso KeyboardState.IsKeyDown(DuplicateKey) AndAlso
               currentHoldTick > LastKeyProcessTick + 2000000 Then
                ProcessKeyPress(DuplicateKey, KeyboardState)
                LastKeyProcessTick = currentHoldTick
            End If
            LastKeyboardState = KeyboardState
        End Sub

        Private Sub ProcessKeyPress(PressedKey As Keys, keyboardState As KeyboardState)
            If DebugEnabled Then
                Select Case PressedKey
                    Case Keys.F1
                        DebugParams.ShowFPS = Not DebugParams.ShowFPS
                    Case Keys.F2
                        DebugParams.ShowDrawFPS = Not DebugParams.ShowDrawFPS
                    Case Keys.F3

                    Case Keys.F4

                    Case Keys.F5

                    Case Keys.OemTilde
                        ConsoleEnabled = Not ConsoleEnabled
                End Select
            End If
            ActiveScene.KeyPress(PressedKey, keyboardState)
            PressedKey = Nothing
        End Sub

        Public Shared Function TryConvertKeyboardInput(key As Keys, keyboard As KeyboardState) As String
            Dim ReturnString As String = String.Empty
            Dim shift As Boolean = (keyboard.IsKeyDown(Keys.LeftShift) Or keyboard.IsKeyDown(Keys.RightShift))
#If WINDOWS Then
            If key >= Keys.A AndAlso key <= Keys.Z AndAlso System.Windows.Forms.Control.IsKeyLocked(System.Windows.Forms.Keys.CapsLock) Then
                shift = Not shift
            End If
#End If

            Select Case key
                Case Keys.Enter
                    ReturnString = ReturnString & Environment.NewLine
                    'Alphabet keys
                Case Keys.A
                    If shift Then
                        ReturnString = ReturnString & "A"
                    Else
                        ReturnString = ReturnString & "a"
                    End If
                Case Keys.B
                    If shift Then
                        ReturnString = ReturnString & "B"
                    Else
                        ReturnString = ReturnString & "b"
                    End If
                Case Keys.C
                    If shift Then
                        ReturnString = ReturnString & "C"
                    Else
                        ReturnString = ReturnString & "c"
                    End If
                Case Keys.D
                    If shift Then
                        ReturnString = ReturnString & "D"
                    Else
                        ReturnString = ReturnString & "d"
                    End If

                Case Keys.E
                    If shift Then
                        ReturnString = ReturnString & "E"
                    Else
                        ReturnString = ReturnString & "e"
                    End If
                Case Keys.F
                    If shift Then
                        ReturnString = ReturnString & "F"
                    Else
                        ReturnString = ReturnString & "f"
                    End If
                Case Keys.G
                    If shift Then
                        ReturnString = ReturnString & "G"
                    Else
                        ReturnString = ReturnString & "g"
                    End If

                Case Keys.H
                    If shift Then
                        ReturnString = ReturnString & "H"
                    Else
                        ReturnString = ReturnString & "h"
                    End If
                Case Keys.I
                    If shift Then
                        ReturnString = ReturnString & "I"
                    Else
                        ReturnString = ReturnString & "i"
                    End If
                Case Keys.J
                    If shift Then
                        ReturnString = ReturnString & "J"
                    Else
                        ReturnString = ReturnString & "j"
                    End If
                Case Keys.K
                    If shift Then
                        ReturnString = ReturnString & "K"
                    Else
                        ReturnString = ReturnString & "k"
                    End If
                Case Keys.L
                    If shift Then
                        ReturnString = ReturnString & "L"
                    Else
                        ReturnString = ReturnString & "l"
                    End If
                Case Keys.M
                    If shift Then
                        ReturnString = ReturnString & "M"
                    Else
                        ReturnString = ReturnString & "m"
                    End If
                Case Keys.N
                    If shift Then
                        ReturnString = ReturnString & "N"
                    Else
                        ReturnString = ReturnString & "n"
                    End If
                Case Keys.O
                    If shift Then
                        ReturnString = ReturnString & "O"
                    Else
                        ReturnString = ReturnString & "o"
                    End If
                Case Keys.P
                    If shift Then
                        ReturnString = ReturnString & "P"
                    Else
                        ReturnString = ReturnString & "p"
                    End If
                Case Keys.Q
                    If shift Then
                        ReturnString = ReturnString & "Q"
                    Else
                        ReturnString = ReturnString & "q"
                    End If
                Case Keys.R
                    If shift Then
                        ReturnString = ReturnString & "R"
                    Else
                        ReturnString = ReturnString & "r"
                    End If
                Case Keys.S
                    If shift Then
                        ReturnString = ReturnString & "S"
                    Else
                        ReturnString = ReturnString & "s"
                    End If
                Case Keys.T
                    If shift Then
                        ReturnString = ReturnString & "T"
                    Else
                        ReturnString = ReturnString & "t"
                    End If
                Case Keys.U
                    If shift Then
                        ReturnString = ReturnString & "U"
                    Else
                        ReturnString = ReturnString & "u"
                    End If
                Case Keys.V
                    If shift Then
                        ReturnString = ReturnString & "V"
                    Else
                        ReturnString = ReturnString & "v"
                    End If
                Case Keys.W
                    If shift Then
                        ReturnString = ReturnString & "W"
                    Else
                        ReturnString = ReturnString & "w"
                    End If
                Case Keys.X
                    If shift Then
                        ReturnString = ReturnString & "X"
                    Else
                        ReturnString = ReturnString & "x"
                    End If
                Case Keys.Y
                    If shift Then
                        ReturnString = ReturnString & "Y"
                    Else
                        ReturnString = ReturnString & "y"
                    End If
                Case Keys.Z
                    If shift Then
                        ReturnString = ReturnString & "Z"
                    Else
                        ReturnString = ReturnString & "z"
                    End If
                    'Decimal keys
                Case Keys.D0
                    If shift Then
                        ReturnString = ReturnString & ")"
                    Else
                        ReturnString = ReturnString & "0"
                    End If
                Case Keys.D1
                    If shift Then
                        ReturnString = ReturnString & "!"
                    Else
                        ReturnString = ReturnString & "1"
                    End If
                Case Keys.D2
                    If shift Then
                        ReturnString = ReturnString & "@"
                    Else
                        ReturnString = ReturnString & "2"
                    End If

                Case Keys.D3
                    If shift Then
                        ReturnString = ReturnString & "#"
                    Else
                        ReturnString = ReturnString & "3"
                    End If
                Case Keys.D4
                    If shift Then
                        ReturnString = ReturnString & "$"
                    Else
                        ReturnString = ReturnString & "4"
                    End If
                Case Keys.D5
                    If shift Then
                        ReturnString = ReturnString & "%"
                    Else
                        ReturnString = ReturnString & "5"
                    End If
                Case Keys.D6
                    If shift Then
                        ReturnString = ReturnString & "^"
                    Else
                        ReturnString = ReturnString & "6"
                    End If
                Case Keys.D7
                    If shift Then
                        ReturnString = ReturnString & "&"
                    Else
                        ReturnString = ReturnString & "7"
                    End If
                Case Keys.D8
                    If shift Then
                        ReturnString = ReturnString & "*"
                    Else
                        ReturnString = ReturnString & "8"
                    End If
                Case Keys.D9
                    If shift Then
                        ReturnString = ReturnString & "("
                    Else
                        ReturnString = ReturnString & "9"
                    End If
                    'Decimal numpad keys
                Case Keys.NumPad0
                    ReturnString = ReturnString & "0"
                Case Keys.NumPad1
                    ReturnString = ReturnString & "1"
                Case Keys.NumPad2
                    ReturnString = ReturnString & "2"
                Case Keys.NumPad3
                    ReturnString = ReturnString & "3"
                Case Keys.NumPad4
                    ReturnString = ReturnString & "4"
                Case Keys.NumPad5
                    ReturnString = ReturnString & "5"
                Case Keys.NumPad6
                    ReturnString = ReturnString & "6"
                Case Keys.NumPad7
                    ReturnString = ReturnString & "7"
                Case Keys.NumPad8
                    ReturnString = ReturnString & "8"
                Case Keys.NumPad9
                    ReturnString = ReturnString & "9"
                    'Special keys
                Case Keys.OemTilde
                    If shift Then
                        ReturnString = ReturnString & "~"
                    Else
                        ReturnString = ReturnString & "`"
                    End If
                Case Keys.OemSemicolon
                    If shift Then
                        ReturnString = ReturnString & ":"
                    Else
                        ReturnString = ReturnString & ";"
                    End If
                Case Keys.OemQuotes
                    If shift Then
                        ReturnString = ReturnString & """"
                    Else
                        ReturnString = ReturnString & "'"
                    End If
                Case Keys.OemQuestion
                    If shift Then
                        ReturnString = ReturnString & "?"
                    Else
                        ReturnString = ReturnString & "/"
                    End If
                Case Keys.OemPlus
                    If shift Then
                        ReturnString = ReturnString & "+"
                    Else
                        ReturnString = ReturnString & "="
                    End If
                Case Keys.OemPipe
                    If shift Then
                        ReturnString = ReturnString & "|"
                    Else
                        ReturnString = ReturnString & "\"
                    End If
                Case Keys.OemPeriod
                    If shift Then
                        ReturnString = ReturnString & ">"
                    Else
                        ReturnString = ReturnString & "."
                    End If
                Case Keys.OemOpenBrackets
                    If shift Then
                        ReturnString = ReturnString & "{"
                    Else
                        ReturnString = ReturnString & "["
                    End If
                Case Keys.OemCloseBrackets
                    If shift Then
                        ReturnString = ReturnString & "}"
                    Else
                        ReturnString = ReturnString & "]"
                    End If
                Case Keys.OemMinus
                    If shift Then
                        ReturnString = ReturnString & "_"
                    Else
                        ReturnString = ReturnString & "-"
                    End If
                Case Keys.OemComma
                    If shift Then
                        ReturnString = ReturnString & "<"
                    Else
                        ReturnString = ReturnString & ","
                    End If
                Case Keys.Space
                    ReturnString = ReturnString & " "
            End Select

            Return ReturnString
        End Function

#End Region

#Region "Mouse"

        Private LastState As MouseState = Nothing
        Private StartPoint As Point = Nothing
        Private MouseDown As Boolean = False
        Private DragThreshold As Integer = 2
        Private ExternalLastPoint As Point = New Point(-1, -1)
        Private ExternalLeftDown As Boolean
        Private ExternalRightDown As Boolean
        Private ExternalScrollWheel As Integer

#If WINDOWS Then

        Private WithEvents MouseHook As MouseHook
        Private LastPoint As Point = Nothing
        Private HardwarePointerInside As Boolean
        Private HardwareRightDown As Boolean
        Private HardwareScrollWheel As Integer

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativePoint
            Public X As Integer
            Public Y As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeRect
            Public Left As Integer
            Public Top As Integer
            Public Right As Integer
            Public Bottom As Integer
        End Structure

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function ScreenToClient(hwnd As IntPtr, ByRef point As NativePoint) As Boolean
        End Function

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function GetClientRect(hwnd As IntPtr, ByRef rect As NativeRect) As Boolean
        End Function

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function GetCursorPos(ByRef point As NativePoint) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function GetForegroundWindow() As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function IsIconic(hwnd As IntPtr) As Boolean
        End Function

        ' Windows-specific: Get actual titlebar and border sizes from System.Windows.Forms
        Private ReadOnly TitlebarHeight As Integer = System.Windows.Forms.SystemInformation.CaptionHeight +
                                                     System.Windows.Forms.SystemInformation.Border3DSize.Height

        Private ReadOnly BorderWidth As Integer = System.Windows.Forms.SystemInformation.Border3DSize.Width +
                                                  System.Windows.Forms.SystemInformation.BorderSize.Width
#Else
#Disable Warning IDE0051, IDE0052 ' Suppress unused member warnings - these are platform stubs
        ' Non-Windows platforms: Default to 0 (no window chrome offset needed)
        Private ReadOnly TitlebarHeight As Integer = 0
        Private ReadOnly BorderWidth As Integer = 0
#Enable Warning IDE0051, IDE0052
#End If

#If WINDOWS Then
        Private Function ToRelativePoint(ms As MouseHook.MSLLHOOKSTRUCT, ByRef insideClient As Boolean) As Point
            ' A low-level mouse hook reports physical screen pixels.  Converting
            ' through the native HWND is DPI-correct across moved/resized windows
            ' and mixed-scale monitors; GameWindow.ClientBounds can be logical.
            Dim native As New NativePoint With {.X = ms.pt.x, .Y = ms.pt.y}
            Dim rect As NativeRect
            Dim handle = Global.ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(Sender)
            If handle <> IntPtr.Zero AndAlso ScreenToClient(handle, native) AndAlso GetClientRect(handle, rect) Then
                Dim width = Math.Max(0, rect.Right - rect.Left)
                Dim height = Math.Max(0, rect.Bottom - rect.Top)
                insideClient = native.X >= 0 AndAlso native.Y >= 0 AndAlso native.X < width AndAlso native.Y < height
                Return ClientToViewport(New Point(native.X, native.Y), width, height)
            End If

            Dim fallback = New Point(ms.pt.x, ms.pt.y).Subtract(Sender.Window.ClientBounds.Location)
            insideClient = fallback.X >= 0 AndAlso fallback.Y >= 0 AndAlso
                           fallback.X < Sender.Window.ClientBounds.Width AndAlso fallback.Y < Sender.Window.ClientBounds.Height
            Return ClientToViewport(fallback)
        End Function

        Private Sub SetHardwareMouseState(point As Point)
            ActiveScene.SetMouseState(New MouseState(
                point.X, point.Y, HardwareScrollWheel,
                If(MouseDown, ButtonState.Pressed, ButtonState.Released),
                ButtonState.Released,
                If(HardwareRightDown, ButtonState.Pressed, ButtonState.Released),
                ButtonState.Released, ButtonState.Released))
        End Sub

        Private Sub MouseHook_LeftButtonDown(mouseStruct As MouseHook.MSLLHOOKSTRUCT) Handles MouseHook.LeftButtonDown
            Dim inside As Boolean
            Dim point = ToRelativePoint(mouseStruct, inside)
            If Not inside Then Return
            StartPoint = point
            MouseDown = True
            HardwarePointerInside = True
            SetHardwareMouseState(point)
            ActiveScene.MouseLeftDown(StartPoint)
        End Sub

        Private Sub MouseHook_LeftButtonUp(mouseStruct As MouseHook.MSLLHOOKSTRUCT) Handles MouseHook.LeftButtonUp
            If Not MouseDown Then Return
            Dim inside As Boolean
            Dim p As Point = ToRelativePoint(mouseStruct, inside)
            MouseDown = False
            SetHardwareMouseState(p)
            ActiveScene.MouseLeftUp(p)
            Dim Difference As Point = GetDifference(StartPoint, p)
            If (Difference.X > DragThreshold) Or (Difference.Y > DragThreshold) Then
                ' Mouse was dragged
                ActiveScene.MouseDragDrop(p, StartPoint)
            Else
                ' Mouse was clicked
                ActiveScene.MouseLeftClick(p)
            End If
        End Sub

        Private Sub MouseHook_MouseMove(mouseStruct As MouseHook.MSLLHOOKSTRUCT) Handles MouseHook.MouseMove
            Dim inside As Boolean
            Dim p As Point = ToRelativePoint(mouseStruct, inside)
            If Not inside AndAlso Not MouseDown Then
                If HardwarePointerInside Then ActiveScene.CancelPointerInput()
                HardwarePointerInside = False
                LastPoint = p
                Return
            End If
            HardwarePointerInside = inside
            SetHardwareMouseState(p)
            If p <> LastPoint Then
                If MouseDown Then
                    Dim Difference As Point = GetDifference(StartPoint, p)
                    If isDragging OrElse ((Difference.X > DragThreshold) Or (Difference.Y > DragThreshold)) Then
                        ' Mouse was dragged
                        isDragging = True
                        ActiveScene.MouseDrag(p, StartPoint)
                    End If
                ElseIf isDragging Then
                    isDragging = False
                End If
                ActiveScene.MouseMove(p, LastPoint)
                LastPoint = p
            End If
        End Sub

        Private Sub MouseHook_RightButtonDown(mouseStruct As MouseHook.MSLLHOOKSTRUCT) Handles MouseHook.RightButtonDown
            Dim inside As Boolean
            Dim point = ToRelativePoint(mouseStruct, inside)
            If Not inside Then Return
            HardwareRightDown = True
            SetHardwareMouseState(point)
            ActiveScene.MouseRightClick(point)
        End Sub

        Private Sub MouseHook_RightButtonUp(mouseStruct As MouseHook.MSLLHOOKSTRUCT) Handles MouseHook.RightButtonUp
            HardwareRightDown = False
            Dim inside As Boolean
            Dim point = ToRelativePoint(mouseStruct, inside)
            If inside Then SetHardwareMouseState(point)
        End Sub

        Private Sub MouseHook_MouseWheel(mouseStruct As MouseHook.MSLLHOOKSTRUCT) Handles MouseHook.MouseWheel
            Dim inside As Boolean
            Dim point = ToRelativePoint(mouseStruct, inside)
            If Not inside Then Return
            Dim bytes = BitConverter.GetBytes(mouseStruct.mouseData)
            Dim delta = CInt(BitConverter.ToInt16(bytes, 2))
            HardwareScrollWheel += delta
            SetHardwareMouseState(point)
            ActiveScene.MouseWheel(delta, point)
        End Sub

#End If

        Private Function TryGetNativeViewportPoint(ByRef point As Point) As Boolean
#If WINDOWS Then
            Dim handle = Global.ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(Sender)
            If handle = IntPtr.Zero OrElse GetForegroundWindow() <> handle OrElse IsIconic(handle) Then Return False
            Dim native As NativePoint
            Dim rect As NativeRect
            If Not GetCursorPos(native) OrElse Not ScreenToClient(handle, native) OrElse Not GetClientRect(handle, rect) Then Return False
            Dim width = Math.Max(0, rect.Right - rect.Left)
            Dim height = Math.Max(0, rect.Bottom - rect.Top)
            If native.X < 0 OrElse native.Y < 0 OrElse native.X >= width OrElse native.Y >= height Then Return False
            point = ClientToViewport(New Point(native.X, native.Y), width, height)
            Return True
#Else
            Return False
#End If
        End Function

        Private Sub DetectMouseEvents(gameTime As GameTime, Optional nativePosition As Boolean = False)
            Dim State As MouseState = Mouse.GetState()
            Dim scaledPosition As Point
            If nativePosition Then
                If Not TryGetNativeViewportPoint(scaledPosition) Then
                    If MouseDown OrElse isDragging Then ActiveScene.CancelPointerInput()
                    MouseDown = False
                    isDragging = False
                    LastState = State
                    Return
                End If
            Else
                scaledPosition = ClientToViewport(State.Position)
            End If
            Dim scaledState As New MouseState(scaledPosition.X, scaledPosition.Y, State.ScrollWheelValue,
                                              State.LeftButton, State.MiddleButton, State.RightButton,
                                              State.XButton1, State.XButton2)
            ActiveScene.SetMouseState(scaledState)

            If State.ScrollWheelValue <> LastState.ScrollWheelValue Then
                ActiveScene.MouseWheel(State.ScrollWheelValue - LastState.ScrollWheelValue, scaledPosition)
            End If

            'Left Button
            If (LastState.LeftButton = ButtonState.Pressed) And (State.LeftButton = ButtonState.Released) Then
                MouseDown = False
                ActiveScene.MouseLeftUp(scaledPosition)
                Dim Difference As Point = GetDifference(StartPoint, scaledPosition)
                If (Difference.X > DragThreshold) Or (Difference.Y > DragThreshold) Then
                    ' Mouse was dragged
                    ActiveScene.MouseDragDrop(scaledPosition, StartPoint)
                Else
                    ' Mouse was clicked
                    ActiveScene.MouseLeftClick(scaledPosition)
                End If
            ElseIf (State.LeftButton = ButtonState.Pressed) And Not MouseDown Then
                StartPoint = scaledPosition
                MouseDown = True
                ActiveScene.MouseLeftDown(scaledPosition)
            End If

            'Right Button
            If LastState.RightButton = ButtonState.Pressed AndAlso State.RightButton = ButtonState.Released Then
                ' Mouse was clicked
                ActiveScene.MouseRightClick(scaledPosition)
            End If

            If scaledPosition <> LastState.Position Then
                If MouseDown Then
                    Dim Difference As Point = GetDifference(StartPoint, scaledPosition)
                    If isDragging OrElse ((Difference.X > DragThreshold) Or (Difference.Y > DragThreshold)) Then
                        ' Mouse was dragged
                        isDragging = True
                        ActiveScene.MouseDrag(scaledPosition, StartPoint)
                    End If
                ElseIf isDragging Then
                    isDragging = False
                End If
                ActiveScene.MouseMove(scaledPosition, LastState.Position)
            End If

            LastState = scaledState
        End Sub

        Public Function ClientToViewport(position As Point) As Point
            ' Get the window's client area size
            Dim clientBounds As Rectangle = Sender.Window.ClientBounds

            Return ClientToViewport(position, clientBounds.Width, clientBounds.Height)
        End Function

        Private Function ClientToViewport(position As Point, clientWidth As Integer, clientHeight As Integer) As Point
            Dim backBufferWidth As Integer = Sender.GraphicsDevice.PresentationParameters.BackBufferWidth
            Dim backBufferHeight As Integer = Sender.GraphicsDevice.PresentationParameters.BackBufferHeight
            ' Avoid division by zero
            If clientWidth <= 0 OrElse clientHeight <= 0 Then Return position

            ' If back buffer matches client bounds, no scaling needed
            If backBufferWidth = clientWidth AndAlso backBufferHeight = clientHeight Then
                Return position
            End If

            ' Mouse position is in window client coordinates
            ' We need to scale it to match the back buffer coordinates
            Dim scaleX As Single = CSng(backBufferWidth) / CSng(clientWidth)
            Dim scaleY As Single = CSng(backBufferHeight) / CSng(clientHeight)

            Return New Point(CInt(position.X * scaleX), CInt(position.Y * scaleY))
        End Function

        ''' <summary>Transforms Project-Z viewport coordinates back to native client pixels.</summary>
        Public Function ViewportToClient(position As Point) As Point
            Dim backBufferWidth = Sender.GraphicsDevice.PresentationParameters.BackBufferWidth
            Dim backBufferHeight = Sender.GraphicsDevice.PresentationParameters.BackBufferHeight
            Dim bounds = Sender.Window.ClientBounds
            If backBufferWidth <= 0 OrElse backBufferHeight <= 0 Then Return position
            Return New Point(
                CInt(position.X * (bounds.Width / CSng(backBufferWidth))),
                CInt(position.Y * (bounds.Height / CSng(backBufferHeight))))
        End Function

        Private Function ScaleExternalMousePosition(position As Point, clientWidth As Integer, clientHeight As Integer) As Point
            If clientWidth <= 0 OrElse clientHeight <= 0 Then Return New Point(-1, -1)
            Dim backBufferWidth As Integer = If(ExternalViewportSize.X > 0, ExternalViewportSize.X,
                                                Sender.GraphicsDevice.PresentationParameters.BackBufferWidth)
            Dim backBufferHeight As Integer = If(ExternalViewportSize.Y > 0, ExternalViewportSize.Y,
                                                 Sender.GraphicsDevice.PresentationParameters.BackBufferHeight)
            Return New Point(
                CInt(position.X * (CSng(backBufferWidth) / clientWidth)),
                CInt(position.Y * (CSng(backBufferHeight) / clientHeight)))
        End Function

        Private Sub SetExternalMouseState(position As Point)
            Dim state As New MouseState(
                position.X, position.Y, ExternalScrollWheel,
                If(ExternalLeftDown, ButtonState.Pressed, ButtonState.Released),
                ButtonState.Released,
                If(ExternalRightDown, ButtonState.Pressed, ButtonState.Released),
                ButtonState.Released, ButtonState.Released)
            ActiveScene.SetMouseState(state)
        End Sub

        Public Sub InjectMouseMove(x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If (Not UseExternalInput AndAlso Not UseHardwareInput) OrElse ActiveScene Is Nothing Then Return
            Dim point As Point = ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight)
            SetExternalMouseState(point)
            If point = ExternalLastPoint Then Return
            If ExternalLeftDown Then
                Dim difference As Point = GetDifference(StartPoint, point)
                If isDragging OrElse difference.X > DragThreshold OrElse difference.Y > DragThreshold Then
                    isDragging = True
                    ActiveScene.MouseDrag(point, StartPoint)
                End If
            ElseIf isDragging Then
                isDragging = False
            End If
            ActiveScene.MouseMove(point, ExternalLastPoint)
            ExternalLastPoint = point
        End Sub

        Public Sub InjectMouseLeftDown(x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If (Not UseExternalInput AndAlso Not UseHardwareInput) OrElse ActiveScene Is Nothing OrElse ExternalLeftDown Then Return
            Dim point As Point = ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight)
            ExternalLeftDown = True
            MouseDown = True
            StartPoint = point
            ExternalLastPoint = point
            SetExternalMouseState(point)
            ActiveScene.MouseLeftDown(point)
        End Sub

        Public Sub InjectMouseLeftUp(x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If (Not UseExternalInput AndAlso Not UseHardwareInput) OrElse ActiveScene Is Nothing OrElse Not ExternalLeftDown Then Return
            Dim point As Point = ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight)
            ExternalLeftDown = False
            MouseDown = False
            SetExternalMouseState(point)
            ActiveScene.MouseLeftUp(point)
            Dim difference As Point = GetDifference(StartPoint, point)
            If isDragging OrElse difference.X > DragThreshold OrElse difference.Y > DragThreshold Then
                ActiveScene.MouseDragDrop(point, StartPoint)
            Else
                ActiveScene.MouseLeftClick(point)
            End If
            isDragging = False
            ExternalLastPoint = point
        End Sub

        Public Sub InjectMouseWheel(x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer, delta As Integer)
            If (Not UseExternalInput AndAlso Not UseHardwareInput) OrElse ActiveScene Is Nothing Then Return
            Dim point = ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight)
            ExternalScrollWheel += delta
            SetExternalMouseState(point)
            ActiveScene.MouseWheel(delta, point)
            ExternalLastPoint = point
        End Sub

        Public Sub CancelInjectedPointer()
            If ActiveScene Is Nothing Then Return
            ExternalLeftDown = False
            ExternalRightDown = False
            MouseDown = False
            isDragging = False
            ExternalLastPoint = New Point(-1, -1)
            ActiveScene.CancelPointerInput()
        End Sub

        Public Sub InjectMouseRightDown(x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If (Not UseExternalInput AndAlso Not UseHardwareInput) OrElse ActiveScene Is Nothing OrElse ExternalRightDown Then Return
            Dim point As Point = ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight)
            ExternalRightDown = True
            SetExternalMouseState(point)
            ActiveScene.MouseRightClick(point)
        End Sub

        Public Sub InjectMouseRightUp(x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If (Not UseExternalInput AndAlso Not UseHardwareInput) OrElse ActiveScene Is Nothing Then Return
            ExternalRightDown = False
            SetExternalMouseState(ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight))
        End Sub

        Public Sub InjectMouseLeave()
            If (Not UseExternalInput AndAlso Not UseHardwareInput) OrElse ActiveScene Is Nothing OrElse ExternalLeftDown Then Return
            Dim outside As New Point(-1, -1)
            SetExternalMouseState(outside)
            ActiveScene.MouseMove(outside, ExternalLastPoint)
            ExternalLastPoint = outside
        End Sub

        Public Sub CancelExternalInput()
            ExternalLeftDown = False
            ExternalRightDown = False
            MouseDown = False
            isDragging = False
            StartPoint = Point.Zero
            ExternalLastPoint = New Point(-1, -1)
            ExternalKeys.Clear()
            If ActiveScene IsNot Nothing Then ActiveScene.CurrentKeyboardState = New KeyboardState()
            If ActiveScene IsNot Nothing Then
                SetExternalMouseState(ExternalLastPoint)
                ActiveScene.CancelPointerInput()
            End If
        End Sub

        Public Sub InjectKeyDown(key As Keys, Optional isRepeat As Boolean = False)
            If Not UseExternalInput OrElse ActiveScene Is Nothing Then Return
            ActiveScene.UsesNativeTextInput = False
            If ExternalKeys.Add(key) Then
                ActiveScene.KeyDown(key, New KeyboardState(ExternalKeys.ToArray()))
            End If
            ProcessKeyPress(key, New KeyboardState(ExternalKeys.ToArray()))
        End Sub

        Public Sub InjectKeyUp(key As Keys)
            If Not UseExternalInput OrElse ActiveScene Is Nothing OrElse Not ExternalKeys.Remove(key) Then Return
            Dim state As New KeyboardState(ExternalKeys.ToArray())
            ActiveScene.KeyUp(key, state)
        End Sub

        Private Function GetDifference(StartPoint As Point, EndPoint As Point) As Point
            Dim DifferenceX As Integer = 0, DifferenceY As Integer = 0
            If StartPoint.X < EndPoint.X Then
                DifferenceX = EndPoint.X - StartPoint.X
            Else
                DifferenceX = StartPoint.X - EndPoint.X
            End If
            If StartPoint.Y < EndPoint.Y Then
                DifferenceY = EndPoint.Y - StartPoint.Y
            Else
                DifferenceY = StartPoint.Y - EndPoint.Y
            End If
            Return New Point(DifferenceX, DifferenceY)
        End Function

#End Region

#End Region

#Region "Debugging"

#Region "Debug Properties"

        Public Property DebugEnabled As Boolean = False
        Public Property ConsoleEnabled As Boolean = False
        Public Property DebugParams As New DebugParameters

        Private _DrawFPS As Integer = 0
        Private LastDrawTick As Long = 0
#End Region

        Private I_DrawFPS As Integer = 0
        Private LastDebugText As String = ""
        Private DebugHeaderRectangle As Rectangle = Nothing
        Private DebugPosition As New Vector2(4, 4)
        Private DebugTextPosition As Vector2 = Nothing
        Private DebugHeaderSize As Vector2 = Nothing
        Private DebugRectangle As Rectangle = Nothing
        Private DebugHeaderColor As Color = Color.LimeGreen
        Private SmallFont As String = Fonts.SegoeUI.GetResourceName(10)
        Private RegularFont As String = Fonts.SegoeUI.GetResourceName(12)
        Private LargeFont As String = Fonts.SegoeUI.GetResourceName(18)

        Private Sub DrawDebugText(gameTime As GameTime)
            If DebugHeaderSize = Nothing Then
                DebugHeaderSize = ActiveScene.MeasureText(LargeFont, Sender.Window.Title)
            End If
            If DebugTextPosition = Nothing Then
                DebugTextPosition = New Vector2(DebugPosition.X, DebugPosition.Y + DebugHeaderSize.Y)
            End If

            Dim DebugText As String = String.Empty

            If DebugParams.ShowFPS Then
                DebugText += String.Format("Tick FPS: {1:D3}{0}", {Environment.NewLine, ActiveScene.FPS})
            End If

            If DebugParams.ShowDrawFPS Then
                DebugText += String.Format("Draw FPS: {1}{0}", {Environment.NewLine, ActiveScene.DrawFPS})
            End If

            If DebugText.EndsWith(Environment.NewLine) Then
                DebugText = DebugText.Remove(DebugText.Length - 1).Trim
            ElseIf DebugText.StartsWith(Environment.NewLine) Then
                DebugText = DebugText.Remove(0, 1).Trim
            End If

            If DebugRectangle = Nothing Or LastDebugText <> DebugText Then
                Dim DebugTextSize As Vector2 = ActiveScene.MeasureText(RegularFont, DebugText)
                Dim LargestWidth As Integer = CInt(If(DebugHeaderSize.X > DebugTextSize.X, DebugHeaderSize.X, DebugTextSize.X))
                Dim RectHeight As Integer = CInt(DebugTextSize.Y)
                DebugRectangle = New Rectangle(CInt(DebugTextPosition.X), CInt(DebugTextPosition.Y), LargestWidth, RectHeight)
            End If

            If DebugHeaderRectangle = Nothing Then
                DebugHeaderRectangle = New Rectangle(CInt(DebugPosition.X), CInt(DebugPosition.Y), DebugRectangle.Width, CInt(DebugHeaderSize.Y))
            End If
            'ActiveScene.spriteBatch.Begin(Graphics.SpriteSortMode.Immediate, Graphics.BlendState.Opaque)
            'ActiveScene.spriteBatch.Draw(ActiveScene.WhitePlain, DebugHeaderRectangle, New Color(40, 40, 40))
            'ActiveScene.spriteBatch.DrawString(ActiveScene.contentCollection.Fonts(LargeFont), Sender.Window.Title, DebugPosition, DebugHeaderColor)

            'If DebugText <> String.Empty Then
            '    ActiveScene.spriteBatch.Draw(ActiveScene.WhitePlain, DebugRectangle, New Color(20, 20, 20))
            '    ActiveScene.spriteBatch.DrawString(ActiveScene.contentCollection.Fonts(RegularFont), DebugText, DebugTextPosition, Color.Snow)
            'End If
            'ActiveScene.spriteBatch.End()
        End Sub

#End Region

        Public Sub Draw(gameTime As GameTime)
            If ActiveScene IsNot Nothing Then
                ActiveScene.Draw(gameTime)
                If DebugEnabled Then
                    I_DrawFPS += 1
                    If (LastDrawTick + OneSecond) < gameTime.TotalGameTime.Ticks Then
                        LastDrawTick = gameTime.TotalGameTime.Ticks
                        _DrawFPS = I_DrawFPS
                        ActiveScene.DrawFPS = _DrawFPS
                        I_DrawFPS = 0
                    End If
                    DrawDebugText(gameTime)
                End If
            End If
        End Sub

        Public Sub Tick(gameTime As GameTime)
            If viewportResizePending Then
                viewportResizePending = False
                ApplyPendingViewportResize()
            End If
            If SetLimit Then
                ' FIX
                'gameTime.ElapsedGameTime =' TimeSpan.FromMilliseconds(1000 / LimitFPS)
            End If

            If ActiveScene IsNot Nothing Then
                ' Input handlers can begin storyboards before Scene.Tick runs.
                ActiveScene.gameTime = gameTime
                If Not UseExternalInput Then
#If WINDOWS Then
                    DrainKeyboardMessages()
#Else
                    DetectKeyPress(gameTime)
#End If
                End If
#If WINDOWS Then
                If Not UseHardwareInput AndAlso Not UseExternalInput Then
                    DetectMouseEvents(gameTime)
                End If
#ElseIf LINUX Then
                DetectMouseEvents(gameTime)
#End If
                ActiveScene.Tick(gameTime)
            End If
        End Sub


        Public Sub AddScene(Name As String, Scene As Scene)
            If Name.Trim = String.Empty Then
                Throw New Exception("Invalid Scene Name.")
            ElseIf Scenes.ContainsKey(Name) Then
                Throw New Exception("The Scene Manager already contains Scene Name '" & Name & "'.")
            Else
                Scenes.Add(Name, Scene)
            End If
        End Sub

        Public Sub RemoveScene(SceneName As String)
            Scenes.Remove(SceneName)
        End Sub

        Public Function GetScene(SceneName As String) As Scene
            Return Scenes(SceneName)
        End Function

        Public Sub New(sender As Game, LimitFPS As Integer)
            Me.Sender = sender
            Me.LimitFPS = LimitFPS
            InitializeViewportResize()
        End Sub

        Public Sub New(sender As Game)
            Me.Sender = sender
            InitializeViewportResize()
            Dim DefaultScene As New DefaultScene(Me)
            Me.AddScene("Default", DefaultScene)
            Me.ActiveScene = DefaultScene
        End Sub

        Private Sub InitializeViewportResize()
            graphicsManager = TryCast(Sender.Services.GetService(GetType(IGraphicsDeviceManager)), GraphicsDeviceManager)
            AddHandler Sender.Window.ClientSizeChanged, AddressOf HandleClientSizeChanged
        End Sub

        Private viewportResizePending As Boolean

        Private Sub HandleClientSizeChanged(senderObject As Object, e As EventArgs)
            If Not resizeInProgress Then viewportResizePending = True
        End Sub

        Private Sub ApplyPendingViewportResize()
            If Not AutoResizeViewport OrElse resizeInProgress OrElse graphicsManager Is Nothing OrElse
               Sender.GraphicsDevice Is Nothing Then Return

            Dim bounds = Sender.Window.ClientBounds
#If WINDOWS Then
            Dim nativeBounds As NativeRect
            Dim nativeHandle = Global.ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(Sender)
            If nativeHandle <> IntPtr.Zero AndAlso GetClientRect(nativeHandle, nativeBounds) Then
                bounds = New Rectangle(0, 0, nativeBounds.Right - nativeBounds.Left, nativeBounds.Bottom - nativeBounds.Top)
            End If
#End If
            If bounds.Width < MinimumViewportSize.X OrElse bounds.Height < MinimumViewportSize.Y Then Return
            Dim presentation = Sender.GraphicsDevice.PresentationParameters
            If presentation.BackBufferWidth = bounds.Width AndAlso presentation.BackBufferHeight = bounds.Height Then Return

            Dim oldSize As New Point(presentation.BackBufferWidth, presentation.BackBufferHeight)
            Dim newSize As New Point(bounds.Width, bounds.Height)
            resizeInProgress = True
            Try
                graphicsManager.PreferredBackBufferWidth = newSize.X
                graphicsManager.PreferredBackBufferHeight = newSize.Y
                graphicsManager.ApplyChanges()
            Finally
                resizeInProgress = False
            End Try

            For Each scene In Scenes.Values
                scene.ResizeViewport(oldSize, newSize)
            Next

            RaiseEvent ViewportResized(oldSize, newSize)
        End Sub

        Public Class DebugParameters

            Public Property ShowFPS As Boolean = True
            Public Property ShowDrawFPS As Boolean = True

        End Class

#Region "IDisposable Support"
        Private disposedValue As Boolean

        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then
                    RemoveHandler Sender.Window.ClientSizeChanged, AddressOf HandleClientSizeChanged
                    ' Dispose all scenes
                    For Each scene In Scenes.Values
                        scene?.Dispose()
                    Next
                    Scenes.Clear()
                    ActiveScene = Nothing

#If WINDOWS Then
                    nativeKeyboard?.ReleaseHandle()
                    nativeKeyboard = Nothing
                    ' Uninstall mouse hook if installed
                    If MouseHook IsNot Nothing Then
                        MouseHook.Uninstall()
                        MouseHook = Nothing
                    End If
#End If
                End If
                disposedValue = True
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region

    End Class

End Namespace
