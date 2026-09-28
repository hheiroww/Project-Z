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
                _SelectionLength = value
                UpdateTextbox()
            End Set
        End Property
        Private _SelectionLength As Integer = 0
        Public Property SelectionStart As Integer
            Get
                Return _SelectionStart
            End Get
            Set(value As Integer)
                _SelectionStart = Math.Max(value, 0)
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
                Dim lineHeight = CSng(Scene.contentCollection.Fonts(Font).LineSpacing)
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

            Selection.spriteBatch.Settings = New SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.Additive)
#If WINDOWS Then
            Selection.FillColor = New Color(0, 0, 1, 0.4)
#ElseIf LINUX Then
            Selection.FillColor = New Color(0, 0, 1, 102)
#End If
            spriteBatch.Settings = New SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                                                             Nothing, Nothing,
                                                             New RasterizerState() With {.ScissorTestEnable = True})
            ' These are visual parts of the textbox, not independent controls.
            ' Let the parent textbox own pointer focus and keyboard selection.
            TextElement.isMouseBypassEnabled = True
            Caret.isMouseBypassEnabled = True
            Selection.isMouseBypassEnabled = True
            Children.AddRange({TextElement, Caret, Selection})
            Clip = True
        End Sub

#End Region

        ' Disable automatic spriteBatch Initialization
        Protected Friend Overrides Sub doDraw(gameTime As GameTime)

            ' Draw Background normally
            If isElementSpriteBatch Then
                spriteBatch.Begin()
                MyBase.Draw(gameTime)
                spriteBatch.End()
            Else
                MyBase.Draw(gameTime)
            End If

            spriteBatch.Begin()
            Draw(gameTime)
            spriteBatch.End()
        End Sub

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            UpdateCaretVisibility()
        End Sub

        Private Sub Textbox_OnKeyPress(Key As Keys, KeyboardState As KeyboardState) Handles Me.OnKeyPress
            ShowCaret()
            Dim PreInputEvent As New PreTextInputEventArgs(Key, KeyboardState)
            RaiseEvent OnPreTextInput(PreInputEvent)
            If Not PreInputEvent.Cancel Then
                Dim control As Boolean = (KeyboardState.IsKeyDown(Keys.LeftControl) Or KeyboardState.IsKeyDown(Keys.RightControl))
                Dim Execute As Boolean = True

                If control Then
                    Select Case Key
                        Case Keys.A
                            SelectionStart = 0
                            SelectionLength = Text.Length
                            CaretPosition = SelectionStart + SelectionLength
                            Execute = False
                        Case Keys.C
#If WINDOWS Then
                            If SelectionLength > 0 AndAlso Not _MaskCharacter.HasValue Then
                                Try
                                    System.Windows.Forms.Clipboard.SetText(
                                        Text.Substring(SelectionStart, Math.Min(SelectionLength, Text.Length - SelectionStart)))
                                Catch
                                End Try
                            End If
#End If
                            Execute = False
                        Case Keys.X
#If WINDOWS Then
                            If SelectionLength > 0 AndAlso Not _MaskCharacter.HasValue Then
                                Try
                                    System.Windows.Forms.Clipboard.SetText(
                                        Text.Substring(SelectionStart, Math.Min(SelectionLength, Text.Length - SelectionStart)))
                                Catch
                                End Try
                            End If
#End If
                            If SelectionLength > 0 Then
                                Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length - SelectionStart))
                                CaretPosition = SelectionStart
                                SelectionLength = 0
                            End If
                            Execute = False
                        Case Keys.V
#If WINDOWS Then
                            Try
                                If System.Windows.Forms.Clipboard.ContainsText() Then
                                    Dim pasted = System.Windows.Forms.Clipboard.GetText().Replace(vbCrLf, vbLf)
                                    If Not AcceptsReturn Then pasted = pasted.Replace(vbCr, " ").Replace(vbLf, " ")
                                    If SelectionLength > 0 Then
                                        Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length - SelectionStart))
                                        CaretPosition = SelectionStart
                                        SelectionLength = 0
                                    End If
                                    Text = Text.Insert(Math.Min(CaretPosition, Text.Length), pasted)
                                    CaretPosition += pasted.Length
                                End If
                            Catch
                            End Try
#End If
                            Execute = False
                    End Select
                End If

                If Execute Then
                    If Key = Keys.Enter AndAlso Not AcceptsReturn Then Return
                    If SelectionLength + SelectionStart > Text.Length Then
                        SelectionStart = 0
                        SelectionLength = 0
                    End If
                    Select Case Key
                        Case Keys.Back
                            If SelectionLength <> 0 Then
                                Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length))
                                CaretPosition = SelectionStart
                            Else
                                If Text.Length > 0 And CaretPosition > 0 Then
                                    Dim oldCaret = CaretPosition
                                    Dim CharCount As Integer = If(oldCaret >= 2 AndAlso Text(oldCaret - 2) = ChrW(13) AndAlso Text(oldCaret - 1) = ChrW(10), 2, 1)
                                    Text = Text.Remove(oldCaret - CharCount, CharCount)
                                    CaretPosition = oldCaret - CharCount
                                End If
                            End If
                            SelectionLength = 0
                        Case Keys.Delete
                            If SelectionLength <> 0 Then
                                Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length))
                                CaretPosition = SelectionStart
                            Else
                                If Text.Length > 0 And CaretPosition < Text.Length Then
                                    Dim CharCount As Integer = If(CaretPosition + 1 < Text.Length AndAlso Text(CaretPosition) = ChrW(13) AndAlso Text(CaretPosition + 1) = ChrW(10), 2, 1)
                                    Text = Text.Remove(CaretPosition, CharCount)
                                End If
                            End If
                            SelectionLength = 0
                        Case Keys.Left
                            If CaretPosition = 0 Then Exit Select
                            If SelectionLength <> 0 Then
                                SelectionLength = 0
                            End If
                            If KeyboardState.IsKeyDown(Keys.LeftControl) Or KeyboardState.IsKeyDown(Keys.RightControl) Then
                                CaretPosition = SelectionStart
                                Exit Select
                            Else
                                CaretPosition -= If(CaretPosition >= 2 AndAlso Text(CaretPosition - 2) = ChrW(13) AndAlso Text(CaretPosition - 1) = ChrW(10), 2, 1)
                            End If
                        Case Keys.Right
                            If Text = Nothing Then Exit Select
                            If SelectionLength <> 0 Then
                                SelectionLength = 0
                            End If
                            If KeyboardState.IsKeyDown(Keys.LeftControl) Or KeyboardState.IsKeyDown(Keys.RightControl) Then
                                CaretPosition = Text.Length
                                Exit Select
                            ElseIf CaretPosition < Text.Length Then
                                Dim CharCount As Integer = If(CaretPosition + 1 < Text.Length AndAlso Text(CaretPosition) = ChrW(13) AndAlso Text(CaretPosition + 1) = ChrW(10), 2, 1)
                                CaretPosition += CharCount
                            End If
                        Case Keys.Home
                            CaretPosition = 0
                            SelectionLength = 0
                        Case Keys.End
                            CaretPosition = Text.Length
                            SelectionLength = 0
                        Case Else
                            Dim newInput As String = SceneManager.TryConvertKeyboardInput(Key, KeyboardState)
                            If newInput <> String.Empty Then
                                If SelectionLength <> 0 Then
                                    Text = Text.Remove(SelectionStart, Math.Min(SelectionLength, Text.Length))
                                    CaretPosition = SelectionStart
                                    Text = Text.Insert(CaretPosition, newInput)
                                    CaretPosition += newInput.Length
                                    SelectionStart = 0
                                    SelectionLength = 0
                                Else
                                    Text = Text.Insert(Math.Min(CaretPosition, Text.Length), newInput)
                                    CaretPosition += newInput.Length
                                End If
                            End If
                    End Select
                End If

            End If
        End Sub

        Private Sub Textbox_OnMouseLeftClick(p As Point) Handles Me.MouseLeftClick
            ShowCaret()
            CaretPosition = inputLayout.HitTest(p.ToVector2() + Position - TextElement.Position, Scene.contentCollection.Fonts(Font).LineSpacing)
            SelectionStart = 0
            SelectionLength = 0
        End Sub

        Private Sub Textbox_OnMouseDrag(currentPoint As Point, startPoint As Point) Handles Me.MouseDrag
            ShowCaret()
            Dim StartIndex = inputLayout.HitTest(startPoint.ToVector2() + Position - TextElement.Position, Scene.contentCollection.Fonts(Font).LineSpacing)
            Dim EndIndex = inputLayout.HitTest(currentPoint.ToVector2() + Position - TextElement.Position, Scene.contentCollection.Fonts(Font).LineSpacing)

            If StartIndex > EndIndex Then
                SelectionStart = EndIndex
                CaretPosition = StartIndex
                SelectionLength = StartIndex - EndIndex
            Else
                SelectionStart = StartIndex
                CaretPosition = EndIndex
                SelectionLength = EndIndex - StartIndex
            End If

        End Sub

        Private Sub Textbox_OnSelect() Handles Me.Selected
            ShowCaret()
        End Sub

        Private Sub Textbox_OnDeselect() Handles Me.Deselected
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
