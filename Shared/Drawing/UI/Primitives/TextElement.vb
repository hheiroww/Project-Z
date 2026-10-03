Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Content
Imports Microsoft.Xna.Framework.Graphics
Imports System.Text

Namespace [Shared].Drawing.UI.Primitives

    ''' <summary>
    ''' Specifies how text wrapping occurs.
    ''' </summary>
    Public Enum TextWrapping
        ''' <summary>
        ''' No wrapping - text extends beyond container bounds.
        ''' </summary>
        NoWrap
        ''' <summary>
        ''' Wraps text at word boundaries to fit within container width.
        ''' </summary>
        Wrap
        ''' <summary>
        ''' Wraps text at character boundaries when words don't fit.
        ''' </summary>
        WrapWithOverflow
    End Enum

    Public Enum TextTrimming
        None
        CharacterEllipsis
        WordEllipsis
    End Enum

    <Serializable>
    Partial Public Class TextElement
        Inherits SceneElement

#Region "Properties"

        Public Property Text As String
            Get
                Return _Text
            End Get
            Set(value As String)
                If _Text <> If(value, String.Empty) Then ClearTextSelection()
                _Text = If(value, String.Empty)
                UpdateWrappedText()
                RaiseEvent TextChanged()
            End Set
        End Property
        Private _Text As String = String.Empty

        ''' <summary>
        ''' Gets or sets the text wrapping behavior.
        ''' </summary>
        Public Property TextWrapping As TextWrapping
            Get
                Return _TextWrapping
            End Get
            Set(value As TextWrapping)
                _TextWrapping = value
                UpdateWrappedText()
            End Set
        End Property
        Private _TextWrapping As TextWrapping = TextWrapping.Wrap

        ''' <summary>
        ''' Gets or sets the maximum width for text wrapping.
        ''' If 0, uses parent container width or no limit.
        ''' </summary>
        Public Property MaxWidth As Single
            Get
                Return _MaxWidth
            End Get
            Set(value As Single)
                _MaxWidth = value
                UpdateWrappedText()
            End Set
        End Property
        Private _MaxWidth As Single = 0

        ''' <summary>
        ''' Gets the wrapped text for rendering.
        ''' </summary>
        Private Property WrappedText As String = String.Empty
        Public Property UseXamlTextLayout As Boolean
        Public Property TextAlignment As HorizontalAlignment = HorizontalAlignment.Left
        Public Property TextTrimming As TextTrimming = TextTrimming.None
        Public Property LineHeight As Single = Single.NaN
        Public Property BlockLineHeight As Boolean
        ' XAML FontSize is in DIPs; the shared font resources use points.
        Public Property FontScale As Single = 1

        Private Function MeasureContent(value As String) As Vector2
            Return Scene.MeasureText(Font, value) * FontScale
        End Function

        Private ReadOnly Property XamlLineSpacing As Single
            Get
                If BlockLineHeight AndAlso Single.IsFinite(LineHeight) AndAlso LineHeight > 0 Then Return LineHeight
                Dim vectorFont As VectorFont = Nothing
                If Scene.contentCollection.VectorFonts.TryGetValue(Font, vectorFont) Then Return vectorFont.LineSpacing * FontScale
                Return Scene.contentCollection.Fonts(Font).LineSpacing * FontScale
            End Get
        End Property

        Public Function MeasureXamlText(availableWidth As Single) As Vector2
            MaxWidth = Math.Max(1, availableWidth - Padding.Left - Padding.Right)
            Return Vector2.Min(XamlDesiredSize, MaxSize)
        End Function

        Friend ReadOnly Property XamlDesiredSize As Vector2
            Get
                Dim content = If(TextWrapping = TextWrapping.NoWrap, Text, WrappedText)
                Dim measured = MeasureContent(If(content, String.Empty))
                If BlockLineHeight AndAlso Not String.IsNullOrEmpty(content) Then
                    measured.Y = content.Replace(vbCr, "").Split(ChrW(10)).Length * XamlLineSpacing
                End If
                Return measured + New Vector2(Padding.Left + Padding.Right, Padding.Top + Padding.Bottom)
            End Get
        End Property

        ''' <summary>
        ''' Prevents recursive updates when Size changes trigger RectangleChanged.
        ''' </summary>
        Private _isUpdatingText As Boolean = False

        Public Property Font As String
            Get
                Return _Font
            End Get
            Set(value As String)
                _Font = value
                UpdateWrappedText()
            End Set
        End Property
        Private _Font As String = Fonts.SegoeUI.GetResourceName(12)

        Public Overridable Property ForegroundColor As New Color(255, 255, 255)

#End Region

#Region "Events"

        Public Event TextChanged()

#End Region

#Region "Text Wrapping"

        ''' <summary>
        ''' Updates the wrapped text based on current settings.
        ''' </summary>
        Private Sub UpdateWrappedText()
            ' Prevent recursive calls
            If _isUpdatingText Then Return
            _isUpdatingText = True

            Try
                ' Skip if Scene is not yet initialized
                If Scene Is Nothing Then
                    WrappedText = _Text
                    Return
                End If

                If _TextWrapping = TextWrapping.NoWrap OrElse String.IsNullOrEmpty(_Text) Then
                    WrappedText = _Text
                    Size = MeasureContent(If(String.IsNullOrEmpty(_Text), " ", _Text))
                    Invalidate()
                    Return
                End If

                ' Determine the wrap width using this priority:
                ' 1. Explicit MaxWidth property
                ' 2. MaxSize.X constraint (from XAML MaxWidth on the element)
                ' 3. Parent available width (minus padding and margin)
                ' 4. No limit
                Dim wrapWidth As Single = _MaxWidth
                If wrapWidth <= 0 AndAlso MaxSize.X < Single.MaxValue Then
                    wrapWidth = MaxSize.X
                End If
                If wrapWidth <= 0 AndAlso Parent IsNot Nothing AndAlso Parent.Size.X > 0 Then
                    wrapWidth = Parent.Size.X - Parent.Padding.Left - Parent.Padding.Right - Margin.Left - Margin.Right
                End If
                If wrapWidth <= 0 Then
                    wrapWidth = 9999.0F ' No limit
                End If

                WrappedText = WrapText(_Text, wrapWidth)
                Dim measuredSize = MeasureContent(If(String.IsNullOrEmpty(WrappedText), " ", WrappedText))
                ' Constrain width so text element never reports wider than its wrap boundary
                Size = New Vector2(Math.Min(measuredSize.X, wrapWidth), measuredSize.Y)
                Invalidate()
            Finally
                _isUpdatingText = False
            End Try
        End Sub

        ''' <summary>
        ''' Wraps text to fit within the specified width.
        ''' </summary>
        Private Function WrapText(text As String, maxWidth As Single) As String
            If String.IsNullOrEmpty(text) Then Return text

            Dim result As New StringBuilder()
            Dim lines As String() = text.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)

            For lineIndex As Integer = 0 To lines.Length - 1
                Dim line As String = lines(lineIndex)
                If lineIndex > 0 Then result.AppendLine()

                If String.IsNullOrEmpty(line) Then Continue For

                Dim lineWidth As Single = MeasureContent(line).X
                If lineWidth <= maxWidth Then
                    result.Append(line)
                    Continue For
                End If

                ' Need to wrap this line
                If _TextWrapping = TextWrapping.Wrap Then
                    result.Append(WrapLineByWords(line, maxWidth))
                Else ' WrapWithOverflow
                    result.Append(WrapLineByCharacters(line, maxWidth))
                End If
            Next

            Return result.ToString()
        End Function

        ''' <summary>
        ''' Wraps a single line by word boundaries.
        ''' </summary>
        Private Function WrapLineByWords(line As String, maxWidth As Single) As String
            Dim result As New StringBuilder()
            Dim words As String() = line.Split(" "c)
            Dim currentLine As New StringBuilder()

            For Each word As String In words
                Dim testLine As String = If(currentLine.Length = 0, word, currentLine.ToString() & " " & word)
                Dim testWidth As Single = MeasureContent(testLine).X

                If testWidth <= maxWidth Then
                    If currentLine.Length > 0 Then currentLine.Append(" ")
                    currentLine.Append(word)
                Else
                    ' Word doesn't fit - start new line
                    If currentLine.Length > 0 Then
                        result.AppendLine(currentLine.ToString())
                        currentLine.Clear()
                    End If

                    ' Check if single word is too long
                    Dim wordWidth As Single = MeasureContent(word).X
                    If wordWidth > maxWidth Then
                        ' Break the word by characters
                        result.Append(WrapLineByCharacters(word, maxWidth))
                        If result.Length > 0 AndAlso Not result.ToString().EndsWith(Environment.NewLine) Then
                            result.AppendLine()
                        End If
                    Else
                        currentLine.Append(word)
                    End If
                End If
            Next

            If currentLine.Length > 0 Then
                result.Append(currentLine.ToString())
            End If

            Return result.ToString().TrimEnd(Environment.NewLine.ToCharArray())
        End Function

        ''' <summary>
        ''' Wraps a single line by character boundaries.
        ''' </summary>
        Private Function WrapLineByCharacters(line As String, maxWidth As Single) As String
            Dim result As New StringBuilder()
            Dim currentLine As New StringBuilder()

            For Each c As Char In line
                Dim testLine As String = currentLine.ToString() & c
                Dim testWidth As Single = MeasureContent(testLine).X

                If testWidth <= maxWidth Then
                    currentLine.Append(c)
                Else
                    ' Character doesn't fit - start new line
                    If currentLine.Length > 0 Then
                        result.AppendLine(currentLine.ToString())
                        currentLine.Clear()
                    End If
                    currentLine.Append(c)
                End If
            Next

            If currentLine.Length > 0 Then
                result.Append(currentLine.ToString())
            End If

            Return result.ToString()
        End Function

        Private Sub OnParentChanged() Handles Me.RectangleChanged
            If _TextWrapping <> TextWrapping.NoWrap Then
                Dim width = _MaxWidth
                If width <= 0 AndAlso MaxSize.X < Single.MaxValue Then width = MaxSize.X
                If width <= 0 AndAlso Parent IsNot Nothing Then width = Parent.Size.X - Parent.Padding.Left - Parent.Padding.Right - Margin.Left - Margin.Right
                If width <> lastGeometryWrapWidth Then
                    lastGeometryWrapWidth = width
                    UpdateWrappedText()
                End If
            End If
        End Sub
        Private lastGeometryWrapWidth As Single = Single.NaN

#End Region

        Public Function PointToCharIndex(p As Point) As Integer
            Dim CurrentX As Integer = CInt(Position.X)
            Dim CurrentY As Integer = CInt(Position.Y)
            Dim LastIndex As Integer = -1

            For i As Integer = 0 To Text.Length - 1
                Dim keyChar As Char = Text(i) : If keyChar = CChar(String.Empty) Then Resume Next
                Dim CharLength As Vector2 = Scene.MeasureText(Font, keyChar)
                Dim CharLengthX As Integer = CInt(CharLength.X)
                Dim CharLengthY As Integer = CInt(CharLength.Y)
                Dim CharRect As New Rectangle(CurrentX, CurrentY, CharLengthX, CharLengthY)

                CurrentX += CharLengthX

                If keyChar.ToString = vbCr Then
                    CurrentY += CInt(CharLength.Y)
                    CurrentX = CInt(Position.X)
                End If

                If CharRect.Contains(p) Then
                    Dim Threshold As Integer = CInt(CharRect.Width / 3)
                    If p.X > (CharRect.X + Threshold) Then
                        Return i + 1
                    Else
                        Return i
                    End If
                Else
                    LastIndex = i + 1
                End If
            Next
            Return LastIndex
        End Function


        Public Function CharIndexToPoint(i As Integer) As Point
            Dim cutText As String = Text.Substring(0, Math.Max(0, Math.Min(i, Text.Length)))
            Dim CharacterHeight As Integer = CInt(Scene.MeasureText(Font, "A").Y)

            Dim TextHeight As Integer = 0
            Dim TextWidth As Integer = 0
            Dim ReturnCuts As String() = cutText.Split(CChar(vbCr))
            Dim LastLine As String = ReturnCuts(ReturnCuts.Length - 1)

            For Each Line As String In ReturnCuts
                TextHeight += CInt(Scene.MeasureText(Font, Line).Y)
            Next

            TextWidth = CInt(Scene.MeasureText(Font, LastLine).X)

            Return New Point(CInt(TextWidth), Math.Max(TextHeight - (CharacterHeight * ReturnCuts.Length), 0))
        End Function

        Public Function CharIndexToLineIndex(i As Integer) As Integer
            ' Dim cutText As String = Text.Substring(0, Math.Max(0, Math.Min(i, Text.Length)))
            'Dim NewLineIndexies As Integer() = cutText.Search(vbCrLf, 0)

            Return 0 'NewLineIndexies.Length - 1
        End Function

        Public Function GetIndexInformation(i As Integer) As IndexInformation
            Return New IndexInformation(i, CharIndexToPoint(i), CharIndexToLineIndex(i))
        End Function

        Private sanitizedSource As String
        Private sanitizedText As String
        Private sanitizedFont As SpriteFont

        ' Use the arranged rectangle, not just the unconstrained wrapped string.
        ' WPF MaxHeight/TextTrimming must affect drawing as well as measurement.
        Private Function FitXamlText(content As String) As String
            Dim lines = content.Replace(vbCr, "").Split(ChrW(10))
            Dim width = Math.Max(0, Math.Min(Size.X, MaxSize.X) - Padding.Left - Padding.Right)
            Dim height = Math.Max(0, Math.Min(Size.Y, MaxSize.Y) - Padding.Top - Padding.Bottom)
            Dim count = Math.Min(lines.Length, CInt(Math.Floor((height + 0.01F) / Math.Max(1, XamlLineSpacing))))
            If count <= 0 Then Return String.Empty
            Dim result As New List(Of String)
            For index = 0 To count - 1
                Dim line = lines(index)
                Dim overflow = MeasureContent(line).X > width OrElse (index = count - 1 AndAlso count < lines.Length)
                If overflow AndAlso TextTrimming <> TextTrimming.None Then
                    Const ellipsis As String = "…"
                    While line.Length > 0 AndAlso MeasureContent(line & ellipsis).X > width
                        line = line.Substring(0, line.Length - 1)
                    End While
                    If TextTrimming = TextTrimming.WordEllipsis AndAlso line.LastIndexOf(" "c) > 0 Then line = line.Substring(0, line.LastIndexOf(" "c))
                    line = If(MeasureContent(ellipsis).X <= width, line.TrimEnd() & ellipsis, String.Empty)
                End If
                result.Add(line)
            Next
            Return String.Join(vbLf, result)
        End Function

        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            Dim textToDraw As String = If(_TextWrapping = TextWrapping.NoWrap, Text, WrappedText)
            If UseXamlTextLayout AndAlso Not String.IsNullOrEmpty(textToDraw) AndAlso
               (TextTrimming <> TextTrimming.None OrElse MaxSize.Y < Single.MaxValue) Then textToDraw = FitXamlText(textToDraw)
            DrawTextSelection(textToDraw)
            If Not String.IsNullOrEmpty(textToDraw) Then
                Dim vectorFont As VectorFont = Nothing
                If Scene.contentCollection.VectorFonts.TryGetValue(Font, vectorFont) Then
                    spriteBatch.End()
                    Try
                        If UseXamlTextLayout Then
                            Dim at = Position + New Vector2(Padding.Left, Padding.Top)
                            Dim width = Math.Max(0, Size.X - Padding.Left - Padding.Right)
                            For Each line In textToDraw.Replace(vbCr, "").Split(ChrW(10))
                                Dim lineWidth = vectorFont.Measure(line).X * FontScale
                                Dim offset = If(TextAlignment = HorizontalAlignment.Center, (width - lineWidth) / 2,
                                    If(TextAlignment = HorizontalAlignment.Right, width - lineWidth, 0))
                                vectorFont.Draw(spriteBatch.GraphicsDevice, line, at + New Vector2(offset, 0), ApplyOpacity(ForegroundColor), Scene.EffectRenderOffset, FontScale)
                                at.Y += XamlLineSpacing
                            Next
                        Else
                            vectorFont.Draw(spriteBatch.GraphicsDevice, textToDraw, Position, ApplyOpacity(ForegroundColor), Scene.EffectRenderOffset)
                        End If
                    Finally
                        spriteBatch.Begin()
                    End Try
                    Return
                End If
                Dim currentFont = Scene.contentCollection.Fonts(Font)
                If textToDraw <> sanitizedSource OrElse sanitizedFont IsNot currentFont Then
                    sanitizedSource = textToDraw
                    sanitizedFont = currentFont
                    sanitizedText = Scene.SanitizeText(Font, textToDraw)
                End If
                If UseXamlTextLayout Then
                    Dim at = Position + New Vector2(Padding.Left, Padding.Top)
                    Dim width = Math.Max(0, Size.X - Padding.Left - Padding.Right)
                    For Each line In sanitizedText.Replace(vbCr, "").Split(ChrW(10))
                        Dim offset = If(TextAlignment = HorizontalAlignment.Center, (width - currentFont.MeasureString(line).X * FontScale) / 2,
                            If(TextAlignment = HorizontalAlignment.Right, width - currentFont.MeasureString(line).X * FontScale, 0))
                        spriteBatch.DrawString(currentFont, line, at + New Vector2(offset, 0), ApplyOpacity(ForegroundColor), 0, Vector2.Zero, FontScale, SpriteEffects.None, 0)
                        at.Y += XamlLineSpacing
                    Next
                Else
                    spriteBatch.DrawString(currentFont, sanitizedText, Position, ApplyOpacity(ForegroundColor))
                End If
            End If
        End Sub

#Region "Constructors"

        Public Sub New(Scene As Scene)
            MyBase.New(Scene)
        End Sub

        Public Sub New(Scene As Scene, spriteBatch As SpriteBatch)
            MyBase.New(Scene, spriteBatch)
        End Sub

        Public Sub New(Scene As Scene, newSpriteBatch As Boolean)
            MyBase.New(Scene, newSpriteBatch)
        End Sub

#End Region

        <Serializable>
        Public Class IndexInformation

            Public Property CharIndex As Integer
            Public Property IndexPosition As Point
            Public Property LineIndex As Integer

            Public Sub New(CharIndex As Integer, IndexPosition As Point, LineIndex As Integer)
                Me.CharIndex = CharIndex
                Me.IndexPosition = IndexPosition
                Me.LineIndex = LineIndex
            End Sub

        End Class

    End Class
End Namespace
