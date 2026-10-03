Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Animations.Properties
Imports Microsoft.Xna.Framework.Input
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Animations.Easing
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Drawing.UI.Primitives

Imports System.Diagnostics
Imports ProjectZ.Shared.XNA
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports System.Collections.Generic

Namespace [Shared].Drawing.UI.Input
    <Serializable>
    Public Class Textbox
        Inherits RectangleElement

#Region "Properties"
        ''' <summary>Allows selection and copy while preventing user edits. Code may still set Text.</summary>
        Private readOnlyValue As Boolean
        Public Property IsReadOnly As Boolean
            Get
                Return readOnlyValue
            End Get
            Set(value As Boolean)
                readOnlyValue = value
                If editMenu IsNot Nothing Then editMenu.IsOpen = False
            End Set
        End Property


        Public Property TextPadding As Vector2
            Get
                Return -_TextPadding
            End Get
            Set(value As Vector2)
                _TextPadding = -value
                UpdateTextbox()
            End Set
        End Property
        Private _TextPadding As New Vector2(-2, -1)
        Public Property HorizontalTextAlignment As HorizontalAlignment
            Get
                Return _HorizontalTextAlignment
            End Get
            Set(value As HorizontalAlignment)
                _HorizontalTextAlignment = CType(value, HorizontalAlignment)
                UpdateTextbox()
            End Set
        End Property
        Private _HorizontalTextAlignment As HorizontalAlignment = HorizontalAlignment.Left
        Public Property VerticalTextAlignment As VerticalAlignment
            Get
                Return _VerticalTextAlignment
            End Get
            Set(value As VerticalAlignment)
                _VerticalTextAlignment = CType(value, VerticalAlignment)
                UpdateTextbox()
            End Set
        End Property
        Private _VerticalTextAlignment As VerticalAlignment = VerticalAlignment.Top
        Public Property AcceptsReturn As Boolean = True
        Public Property SelectionLength As Integer
            Get
                Return _SelectionLength
            End Get
            Set(value As Integer)
                _SelectionLength = Math.Clamp(value, 0, Text.Length - _SelectionStart)
                selectionAnchor = If(_CaretPosition = _SelectionStart, _SelectionStart + _SelectionLength, _SelectionStart)
                UpdateTextbox()
            End Set
        End Property
        Private _SelectionLength As Integer = 0
        Public Property SelectionStart As Integer
            Get
                Return _SelectionStart
            End Get
            Set(value As Integer)
                _SelectionStart = Math.Clamp(value, 0, Text.Length)
                _SelectionLength = Math.Min(_SelectionLength, Text.Length - _SelectionStart)
                selectionAnchor = _SelectionStart
                UpdateTextbox()
            End Set
        End Property
        Private _SelectionStart As Integer = 0
        Public Property CaretPosition As Integer
            Get
                Return _CaretPosition
            End Get
            Set(value As Integer)
                _CaretPosition = Math.Min(Text.Length, Math.Max(0, value))
                UpdateTextbox()
            End Set
        End Property
        Private _CaretPosition As Integer = 0
        Public Property Text As String
            Get
                Return _Text
            End Get
            Set(value As String)
                value = If(value, String.Empty)
                If _Text = value Then Return
                Dim previous = _Text
                _Text = value
                _CaretPosition = Math.Min(_CaretPosition, _Text.Length)
                _SelectionStart = Math.Min(_SelectionStart, _Text.Length)
                _SelectionLength = Math.Min(_SelectionLength, _Text.Length - _SelectionStart)
                selectionAnchor = Math.Min(selectionAnchor, _Text.Length)
                UpdateTextbox()
                RaiseEvent OnTextChanged(New TextChangedEventArgs(previous, value))
            End Set
        End Property
        Private _Text As String = String.Empty
        ''' <summary>
        ''' Optional character used when rendering sensitive text. The actual
        ''' value remains available through <see cref="Text"/> while selection,
        ''' caret movement, and editing continue to use the real string.
        ''' </summary>
        Public Property MaskCharacter As Char?
            Get
                Return _MaskCharacter
            End Get
            Set(value As Char?)
                _MaskCharacter = value
                UpdateTextbox()
            End Set
        End Property
        Private _MaskCharacter As Char? = Nothing
        Public Property Font As String
            Get
                Return _Font
            End Get
            Set(value As String)
                _Font = value
                _FontCharHeight = Scene.MeasureText(_Font, "A").Y
                UpdateTextbox()
            End Set
        End Property
        Private _Font As String = Content.Fonts.SegoeUI.GetResourceName(12)
        Private _FontCharHeight As Single = 0
        Public Property ForegroundColor As Color
            Get
                Return _ForegroundColor
            End Get
            Set(value As Color)
                _ForegroundColor = value
                TextElement.ForegroundColor = _ForegroundColor
                Caret.BackgroundColor = _ForegroundColor
            End Set
        End Property
        Private _ForegroundColor As Color = Color.White
        Public Overrides Property BackgroundColor As Color
            Get
                Return _BackgroundColor
            End Get
            Set(value As Color)
                _BackgroundColor = value
            End Set
        End Property
        Private _BackgroundColor As New Color(50, 50, 50)

#End Region

#Region "Internals"

        Private canNotUpdate As Boolean = False
        Private inputLayout As TextInputLayout
        Private layoutText, layoutFont As String
        Private layoutWidth As Single = -1
        Private textScroll As Vector2
        Private selectionAnchor As Integer
        Private draggingSelection As Boolean
        Private dragPoint As Point
        Public Property SelectionColor As Color = New Color(38, 93, 160)
        Public ReadOnly Property SelectedText As String
            Get
                Return Text.Substring(SelectionStart, SelectionLength)
            End Get
        End Property
        Public ReadOnly Property SelectionBounds As IReadOnlyList(Of Rectangle)
            Get
                If inputLayout Is Nothing Then Return Array.Empty(Of Rectangle)()
                Dim result As New List(Of Rectangle)
                Dim area As New Rectangle(CInt(Position.X + TextPadding.X), CInt(Position.Y + TextPadding.Y),
                    CInt(Math.Max(0, Size.X - TextPadding.X * 2)), CInt(Math.Max(0, Size.Y - TextPadding.Y * 2)))
                For Each bounds In inputLayout.SelectionRectangles(SelectionStart, SelectionLength)
                    bounds.Offset(CInt(TextElement.Position.X), CInt(TextElement.Position.Y))
                    bounds = Rectangle.Intersect(bounds, area)
                    If bounds.Width > 0 AndAlso bounds.Height > 0 Then result.Add(bounds)
                Next
                Return result
            End Get
        End Property
        Public ReadOnly Property CaretBounds As Rectangle
            Get
                Return Caret.Rectangle
            End Get
        End Property
        Private Function GetAlignment(RelativeTo As SceneElement) As Vector2
            Dim RelativeSizeY As Single = Math.Max(_FontCharHeight, RelativeTo.Size.Y)
            Select Case VerticalTextAlignment
                Case VerticalAlignment.Center
                    GetAlignment.Y = (Size.Y / 2 - RelativeSizeY / 2)
                Case VerticalAlignment.Bottom
                    GetAlignment.Y = (Size.Y - RelativeSizeY)
                Case Else
                    GetAlignment.Y = 0
            End Select
            Select Case HorizontalTextAlignment
                Case HorizontalAlignment.Center
                    GetAlignment.X = (Size.X / 2 - RelativeTo.Size.X / 2)
                Case HorizontalAlignment.Right
                    GetAlignment.X = (Size.X - RelativeTo.Size.X)
                Case Else
                    GetAlignment.X = 0
            End Select
        End Function

        Private Sub UpdateTextbox() Handles Me.RectangleChanged
            If canNotUpdate OrElse TextElement Is Nothing OrElse Caret Is Nothing OrElse Selection Is Nothing Then Return

            canNotUpdate = True
            Try
                Dim display = If(_MaskCharacter.HasValue, New String(_MaskCharacter.Value, Text.Length), Text)
                Dim available = New Vector2(Math.Max(1, Size.X - TextPadding.X * 2 - 1), Math.Max(1, Size.Y - TextPadding.Y * 2))
                Dim wrapWidth = If(AcceptsReturn, available.X, Single.MaxValue)
                Dim lineHeight = CSng(Scene.TextLineSpacing(Font))
                If inputLayout Is Nothing OrElse display <> layoutText OrElse Font <> layoutFont OrElse wrapWidth <> layoutWidth Then
                    inputLayout = New TextInputLayout(display, wrapWidth, lineHeight, Function(line) Scene.MeasureText(Font, line).X)
                    layoutText = display
                    layoutFont = Font
                    layoutWidth = wrapWidth
                    TextElement.TextWrapping = TextWrapping.NoWrap
                    TextElement.Font = Font
                    TextElement.Text = inputLayout.DisplayText
                End If
                Dim insertion = inputLayout.Points(Math.Min(CaretPosition, inputLayout.Points.Length - 1))
                textScroll.X = Math.Max(0, Math.Min(textScroll.X, insertion.X))
                textScroll.Y = Math.Max(0, Math.Min(textScroll.Y, insertion.Y))
                textScroll.X = Math.Max(textScroll.X, insertion.X - available.X)
                textScroll.Y = Math.Max(textScroll.Y, insertion.Y + lineHeight - available.Y)
                Dim alignment = GetAlignment(TextElement)
                If HorizontalTextAlignment = HorizontalAlignment.Left OrElse TextElement.Size.X > available.X Then alignment.X = TextPadding.X
                If VerticalTextAlignment = VerticalAlignment.Top OrElse inputLayout.Height > available.Y Then alignment.Y = TextPadding.Y
                TextElement.Position = Position + alignment - textScroll
                Caret.Position = TextElement.Position + insertion
                Caret.Size = New Vector2(1, lineHeight)
                UpdateCaretVisibility()
                Selection.ClearVectorPoints()
            Finally
                canNotUpdate = False
            End Try
        End Sub

        Protected Overrides Sub AlignChildren()
            ' Generic container layout would put the caret back at the origin.
            UpdateTextbox()
        End Sub

#End Region

#Region "Events"

        Public Event OnPreTextInput(e As PreTextInputEventArgs)

        Public Event OnTextChanged(e As TextChangedEventArgs)

#End Region

#Region "Animation Properties"

        Public ForegroundProperty As ForegroundColorProperty

#Region "Animation Instances"
        Private Const CaretBlinkIntervalMilliseconds As Long = 530
        Private caretBlinkStarted As Long

        Private Sub ShowCaret()
            caretBlinkStarted = Environment.TickCount64
            Caret.BackgroundColor = _ForegroundColor
        End Sub

        Private Sub HideCaret()
            Caret.BackgroundColor = Color.Transparent
        End Sub

        Private Sub UpdateCaretVisibility()
            If Not CanSelect OrElse Not isSelected Then
                Caret.BackgroundColor = Color.Transparent
                Return
            End If
            Dim elapsed = Math.Max(0, Environment.TickCount64 - caretBlinkStarted)
            Caret.BackgroundColor = If((elapsed \ CaretBlinkIntervalMilliseconds) Mod 2 = 0,
                                       _ForegroundColor, Color.Transparent)
        End Sub

#End Region

#End Region

#Region "Child Elements"

        Protected Friend TextElement As New TextElement(Scene, spriteBatch.SpriteBatch)
        Protected Friend Caret As New RectangleElement(Scene, spriteBatch.SpriteBatch)
        Protected Friend Selection As New PolygonElement(Scene, {}, spriteBatch.SpriteBatch)

#End Region

#Region "Constructors"

        Public Sub New(Scene As Scene)
            MyBase.New(Scene, True)

            BackgroundProperty = New BackgroundColorProperty(Me)
            ForegroundProperty = New ForegroundColorProperty(TextElement)
            CanSelect = True
            _FontCharHeight = Scene.MeasureText(_Font, "A").Y

            ' These are visual parts of the textbox, not independent controls.
            ' Let the parent textbox own pointer focus and keyboard selection.
            TextElement.isMouseBypassEnabled = True
            Caret.isMouseBypassEnabled = True
            Selection.isMouseBypassEnabled = True
            Children.AddRange({TextElement, Caret, Selection})
            Clip = True
        End Sub

#End Region

        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            MyBase.Draw(gameTime)
            Dim fill = ApplyOpacity(If(isSelected, SelectionColor, New Color(65, 75, 85)))
            For Each bounds In SelectionBounds
                spriteBatch.Draw(Texture, bounds, fill)
            Next
        End Sub

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            If draggingSelection AndAlso (dragPoint.X < TextPadding.X OrElse dragPoint.X > Size.X - TextPadding.X OrElse
                dragPoint.Y < TextPadding.Y OrElse dragPoint.Y > Size.Y - TextPadding.Y) Then
                MoveCaret(HitIndex(dragPoint), True)
            End If
            UpdateCaretVisibility()
        End Sub

        Public Sub [Select](start As Integer, length As Integer)
            _SelectionStart = Math.Clamp(start, 0, Text.Length)
            _SelectionLength = Math.Clamp(length, 0, Text.Length - _SelectionStart)
            selectionAnchor = _SelectionStart
            _CaretPosition = _SelectionStart + _SelectionLength
            ShowCaret()
            UpdateTextbox()
        End Sub

        Public Sub SelectAll()
            [Select](0, Text.Length)
        End Sub

        Private editMenu As ContextMenu

        Private Sub OpenEditMenu(point As Point) Handles Me.MouseRightClick
            If editMenu IsNot Nothing Then
                Scene.RemoveElement(editMenu)
                editMenu.Dispose()
                editMenu = Nothing
            End If
            If editMenu Is Nothing Then
                editMenu = New ContextMenu(Scene) With {
                    .Size = New Vector2(168, If(IsReadOnly OrElse TypeOf Me Is PasswordBox, 72, 136)),
                    .BackgroundColor = New Color(17, 23, 28)}
                Dim labels = If(IsReadOnly, If(TypeOf Me Is PasswordBox, {"Select all"}, {"Copy", "Select all"}), If(TypeOf Me Is PasswordBox, {"Paste", "Select all"}, {"Cut", "Copy", "Paste", "Select all"}))
                For Each value In labels
                    Dim label = value
                    Dim item As New MenuItem(Scene) With {.Header = label, .AutoSize = ButtonAutoSize.None,
                        .Size = New Vector2(160, 32), .ForegroundColor = Color.White,
                        .BackgroundColor = New Color(17, 23, 28), .MouseOverBackgroundColor = New Color(42, 55, 49)}
                    AddHandler item.MouseLeftClick, Sub(p)
                                                        editMenu.IsOpen = False
                                                        Select Case label
                                                            Case "Cut" : CutSelection()
                                                            Case "Copy" : CopySelection()
                                                            Case "Paste" : PasteClipboard()
                                                            Case "Select all" : SelectAll()
                                                        End Select
                                                        Scene.FocusElement(Me)
                                                    End Sub
                    editMenu.AddItem(item)
                Next
                Scene.AddElement(editMenu)
            End If
            ' Element mouse events are local; the overlay menu is scene-relative.
            Dim viewport = Scene.InputViewportSize
            editMenu.Position = New Vector2(
                Math.Clamp(Position.X + point.X, 0, Math.Max(0, viewport.X - editMenu.Size.X)),
                Math.Clamp(Position.Y + point.Y, 0, Math.Max(0, viewport.Y - editMenu.Size.Y)))
            editMenu.IsOpen = True
        End Sub

        Public Function CopySelection() As Boolean
            If SelectionLength = 0 OrElse MaskCharacter.HasValue OrElse TypeOf Me Is PasswordBox Then Return False
#If WINDOWS Then
            Try
                System.Windows.Forms.Clipboard.SetText(SelectedText)
                Return True
            Catch ex As System.Runtime.InteropServices.ExternalException
                Return False
            End Try
#Else
            Return False
#End If
        End Function

        Public Function CutSelection() As Boolean
            If IsReadOnly Then Return False
            If Not CopySelection() Then Return False
            ReplaceSelection(String.Empty)
            Return True
        End Function

        Public Function PasteClipboard() As Boolean
            If IsReadOnly Then Return False
#If WINDOWS Then
            Try
                If Not System.Windows.Forms.Clipboard.ContainsText() Then Return False
                ReplaceSelection(System.Windows.Forms.Clipboard.GetText())
                Return True
            Catch ex As System.Runtime.InteropServices.ExternalException
                Return False
            End Try
#Else
            Return False
#End If
        End Function

        Public Sub ReplaceSelection(value As String)
            If IsReadOnly Then Return
            value = If(value, String.Empty)
            If Not AcceptsReturn Then value = value.Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ")
            Dim start = If(SelectionLength > 0, SelectionStart, CaretPosition)
            Dim previous = _Text
            _Text = _Text.Remove(start, SelectionLength).Insert(start, value)
            _CaretPosition = start + value.Length
            _SelectionStart = _CaretPosition
            _SelectionLength = 0
            selectionAnchor = _CaretPosition
            ShowCaret()
            UpdateTextbox()
            If previous <> _Text Then RaiseEvent OnTextChanged(New TextChangedEventArgs(previous, _Text))
        End Sub

        Private Sub MoveCaret(position As Integer, extend As Boolean)
            position = Math.Clamp(position, 0, Text.Length)
            If position > 0 AndAlso position < Text.Length AndAlso Text(position - 1) = ChrW(13) AndAlso Text(position) = ChrW(10) Then position += 1
            If extend Then
                If SelectionLength = 0 Then selectionAnchor = CaretPosition
                _SelectionStart = Math.Min(selectionAnchor, position)
                _SelectionLength = Math.Abs(position - selectionAnchor)
            Else
                selectionAnchor = position
                _SelectionStart = position
                _SelectionLength = 0
            End If
            _CaretPosition = position
            ShowCaret()
            UpdateTextbox()
        End Sub

        Private Function PreviousPosition(position As Integer) As Integer
            If position <= 0 Then Return 0
            If position >= 2 AndAlso ((Text(position - 2) = ChrW(13) AndAlso Text(position - 1) = ChrW(10)) OrElse
                (Char.IsHighSurrogate(Text(position - 2)) AndAlso Char.IsLowSurrogate(Text(position - 1)))) Then Return position - 2
            Return position - 1
        End Function

        Private Function NextPosition(position As Integer) As Integer
            If position >= Text.Length Then Return Text.Length
            If position + 1 < Text.Length AndAlso ((Text(position) = ChrW(13) AndAlso Text(position + 1) = ChrW(10)) OrElse
                (Char.IsHighSurrogate(Text(position)) AndAlso Char.IsLowSurrogate(Text(position + 1)))) Then Return position + 2
            Return position + 1
        End Function

        Private Function WordPosition(position As Integer, backwards As Boolean) As Integer
            If backwards Then
                While position > 0 AndAlso Char.IsWhiteSpace(Text(position - 1))
                    position = PreviousPosition(position)
                End While
                If position = 0 Then Return 0
                Dim word = Char.IsLetterOrDigit(Text(position - 1)) OrElse Text(position - 1) = "_"c
                While position > 0 AndAlso Not Char.IsWhiteSpace(Text(position - 1)) AndAlso
                    (Char.IsLetterOrDigit(Text(position - 1)) OrElse Text(position - 1) = "_"c) = word
                    position = PreviousPosition(position)
                End While
            Else
                If position >= Text.Length Then Return Text.Length
                Dim word = Char.IsLetterOrDigit(Text(position)) OrElse Text(position) = "_"c
                While position < Text.Length AndAlso Not Char.IsWhiteSpace(Text(position)) AndAlso
                    (Char.IsLetterOrDigit(Text(position)) OrElse Text(position) = "_"c) = word
                    position = NextPosition(position)
                End While
                While position < Text.Length AndAlso Char.IsWhiteSpace(Text(position))
                    position = NextPosition(position)
                End While
            End If
            Return position
        End Function

        Private Sub Textbox_OnKeyPress(Key As Keys, KeyboardState As KeyboardState) Handles Me.OnKeyPress
            ShowCaret()
            Dim preInput As New PreTextInputEventArgs(Key, KeyboardState)
            RaiseEvent OnPreTextInput(preInput)
            If preInput.Cancel Then Return
            Dim control = KeyboardState.IsKeyDown(Keys.LeftControl) OrElse KeyboardState.IsKeyDown(Keys.RightControl)
            Dim shift = KeyboardState.IsKeyDown(Keys.LeftShift) OrElse KeyboardState.IsKeyDown(Keys.RightShift)
            If control Then
                Select Case Key
                    Case Keys.A : SelectAll() : Return
                    Case Keys.C, Keys.Insert : CopySelection() : Return
                    Case Keys.X : CutSelection() : Return
                    Case Keys.V : PasteClipboard() : Return
                End Select
            End If
            If shift AndAlso Key = Keys.Insert Then PasteClipboard() : Return
            If shift AndAlso Key = Keys.Delete AndAlso SelectionLength > 0 Then CutSelection() : Return
            Select Case Key
                Case Keys.Left, Keys.Right
                    Dim backwards = Key = Keys.Left
                    Dim destination As Integer
                    If Not shift AndAlso SelectionLength > 0 Then
                        destination = If(backwards, SelectionStart, SelectionStart + SelectionLength)
                    ElseIf control Then
                        destination = WordPosition(CaretPosition, backwards)
                    Else
                        destination = If(backwards, PreviousPosition(CaretPosition), NextPosition(CaretPosition))
                    End If
                    MoveCaret(destination, shift)
                Case Keys.Home, Keys.End
                    Dim destination = If(control, If(Key = Keys.Home, 0, Text.Length), inputLayout.LineBoundary(CaretPosition, Key = Keys.End))
                    MoveCaret(destination, shift)
                Case Keys.Up, Keys.Down
                    Dim point = inputLayout.Points(CaretPosition)
                    point.Y += If(Key = Keys.Up, -1, 1) * Scene.TextLineSpacing(Font)
                    MoveCaret(inputLayout.HitTest(point, Scene.TextLineSpacing(Font)), shift)
                Case Keys.Back, Keys.Delete
                    If IsReadOnly Then Return
                    If SelectionLength = 0 Then
                        Dim destination = If(Key = Keys.Back, If(control, WordPosition(CaretPosition, True), PreviousPosition(CaretPosition)),
                            If(control, WordPosition(CaretPosition, False), NextPosition(CaretPosition)))
                        MoveCaret(destination, True)
                    End If
                    ReplaceSelection(String.Empty)
                Case Else
                    If control OrElse KeyboardState.IsKeyDown(Keys.LeftAlt) OrElse KeyboardState.IsKeyDown(Keys.RightAlt) Then Return
                    If Key = Keys.Enter AndAlso Not AcceptsReturn Then Return
                    If Scene.UsesNativeTextInput AndAlso Key <> Keys.Enter Then Return
                    Dim value = SceneManager.TryConvertKeyboardInput(Key, KeyboardState)
                    If value <> String.Empty Then ReplaceSelection(value)
            End Select
        End Sub

        Private Function HitIndex(point As Point) As Integer
            UpdateTextbox()
            Return inputLayout.HitTest(point.ToVector2() + Position - TextElement.Position, Scene.TextLineSpacing(Font))
        End Function

        Private Sub Textbox_OnMouseLeftDown(point As Point) Handles Me.MouseLeftDown
            Dim modifiers = Scene.CurrentKeyboardState
            Dim extend = modifiers.IsKeyDown(Keys.LeftShift) OrElse modifiers.IsKeyDown(Keys.RightShift)
            MoveCaret(HitIndex(point), extend)
            draggingSelection = True
            dragPoint = point
        End Sub

        Private Sub Textbox_OnMouseLeftUp(point As Point) Handles Me.MouseLeftUp
            draggingSelection = False
        End Sub

        Private Sub Textbox_OnMouseDrag(currentPoint As Point, startPoint As Point) Handles Me.MouseDrag
            dragPoint = currentPoint
            MoveCaret(HitIndex(currentPoint), True)
        End Sub

        Private Sub Textbox_OnSelect() Handles Me.Selected
            ShowCaret()
        End Sub

        Private Sub Textbox_OnDeselect() Handles Me.Deselected
            draggingSelection = False
            HideCaret()
        End Sub

    End Class

    Public Class PreTextInputEventArgs

        Public ReadOnly Property Key As Keys
            Get
                Return _Key
            End Get
        End Property
        Private _Key As Keys

        Public ReadOnly Property KeyboardState As KeyboardState
            Get
                Return _KeyboardState
            End Get
        End Property
        Private _KeyboardState As KeyboardState

        Public Property Cancel As Boolean = False

        Public Sub New(Key As Keys, KeyboardState As KeyboardState)
            _Key = Key
            _KeyboardState = KeyboardState
        End Sub

    End Class

    Public Class TextChangedEventArgs

        Public ReadOnly Property OldText As String
            Get
                Return _OldText
            End Get
        End Property
        Private _OldText As String

        Public ReadOnly Property NewText As String
            Get
                Return _NewText
            End Get
        End Property
        Private _NewText As String

        Public Sub New(Oldtext As String, NewText As String)
            _OldText = Oldtext
            _NewText = NewText
        End Sub

    End Class

End Namespace
