Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Drawing.UI.Input

Namespace [Shared].Drawing.UI.Primitives
    Partial Public Class TextElement
        Private selectionEnabled As Boolean
        Private selectionAnchor As Integer
        Private selectionEnd As Integer
        Private selectionTexture As Texture2D
        Private selectionMenu As ContextMenu

        ''' <summary>Opt in to selecting and copying display text. This never enables editing.</summary>
        Public Property IsTextSelectionEnabled As Boolean
            Get
                Return selectionEnabled
            End Get
            Set(value As Boolean)
                selectionEnabled = value
                CanSelect = value
                If value Then isMouseBypassEnabled = False
                If Not value Then
                    ClearTextSelection()
                    If selectionMenu IsNot Nothing Then selectionMenu.IsOpen = False
                End If
            End Set
        End Property
        Public ReadOnly Property SelectionStart As Integer
            Get
                Return Math.Min(selectionAnchor, selectionEnd)
            End Get
        End Property
        Public ReadOnly Property SelectionLength As Integer
            Get
                Return Math.Abs(selectionEnd - selectionAnchor)
            End Get
        End Property
        Public ReadOnly Property SelectedText As String
            Get
                Return Text.Substring(SelectionStart, SelectionLength)
            End Get
        End Property
        Public Property SelectionColor As Color = New Color(38, 93, 160)
        Private Sub ClearTextSelection()
            selectionAnchor = 0
            selectionEnd = 0
        End Sub
        Public Sub [Select](start As Integer, length As Integer)
            If Not IsTextSelectionEnabled Then Return
            selectionAnchor = Math.Clamp(start, 0, Text.Length)
            selectionEnd = Math.Clamp(selectionAnchor + Math.Max(0, length), 0, Text.Length)
        End Sub
        Public Sub SelectAll()
            [Select](0, Text.Length)
        End Sub
        Public Function CopySelection() As Boolean
            If Not IsTextSelectionEnabled OrElse SelectionLength = 0 Then Return False
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

        ' Use the same displayed lines, scale and alignment as Draw; indices remain in source text.
        Private Function SelectionGlyphs(content As String) As List(Of (Index As Integer, Bounds As Rectangle))
            Dim result As New List(Of (Index As Integer, Bounds As Rectangle))
            Dim cursor = 0
            Dim y As Single = If(UseXamlTextLayout, Padding.Top, 0)
            Dim scale = If(UseXamlTextLayout, FontScale, 1.0F)
            Dim spacing = If(UseXamlTextLayout, XamlLineSpacing, Scene.TextLineSpacing(Font))
            Dim sourceLines = If(If(TextWrapping = TextWrapping.NoWrap, Text, WrappedText), String.Empty).Replace(vbCr, "").Split(ChrW(10))
            Dim lineNumber = 0
            For Each line In If(content, String.Empty).Replace(vbCr, "").Split(ChrW(10))
                Dim originalLine = sourceLines(Math.Min(lineNumber, sourceLines.Length - 1))
                Dim indices As New List(Of Integer)
                For Each character In originalLine
                    While cursor < Text.Length AndAlso Text(cursor) <> character AndAlso Char.IsWhiteSpace(Text(cursor))
                        cursor += 1
                    End While
                    indices.Add(cursor)
                    If cursor < Text.Length Then cursor += 1
                Next
                lineNumber += 1
                Dim width = Scene.MeasureText(Font, line).X * scale
                Dim available = Math.Max(0, Size.X - Padding.Left - Padding.Right)
                Dim x As Single = If(UseXamlTextLayout, Padding.Left + If(TextAlignment = HorizontalAlignment.Center, (available - width) / 2, If(TextAlignment = HorizontalAlignment.Right, available - width, 0)), 0)
                For i = 0 To line.Length - 1
                    If i >= indices.Count OrElse indices(i) >= Text.Length OrElse Text(indices(i)) <> line(i) Then Exit For ' synthetic ellipsis
                    Dim left = Scene.MeasureText(Font, line.Substring(0, i)).X * scale
                    Dim right = Scene.MeasureText(Font, line.Substring(0, i + 1)).X * scale
                    result.Add((indices(i), New Rectangle(CInt(Math.Floor(x + left)), CInt(Math.Floor(y)), Math.Max(1, CInt(Math.Ceiling(right - left))), CInt(Math.Ceiling(spacing)))))
                Next
                y += spacing
            Next
            Return result
        End Function
        Private Function SelectionContent() As String
            Dim content = If(TextWrapping = TextWrapping.NoWrap, Text, WrappedText)
            If UseXamlTextLayout AndAlso (TextTrimming <> TextTrimming.None OrElse MaxSize.Y < Single.MaxValue) Then content = FitXamlText(content)
            Return content
        End Function
        Private Function SelectionHit(point As Point) As Integer
            Dim glyphs = SelectionGlyphs(SelectionContent())
            If glyphs.Count = 0 Then Return 0
            Dim closest = glyphs.OrderBy(Function(g) Math.Abs(g.Bounds.Center.Y - point.Y)).ThenBy(Function(g) Math.Abs(g.Bounds.Center.X - point.X)).First()
            Dim index = closest.Index + If(point.X >= closest.Bounds.Center.X, 1, 0)
            If index > 0 AndAlso index < Text.Length AndAlso Char.IsLowSurrogate(Text(index)) Then index += 1
            Return index
        End Function
        Private Sub BeginTextSelection(point As Point) Handles Me.MouseLeftDown
            If Not IsTextSelectionEnabled Then Return
            Dim state = Scene.CurrentKeyboardState
            If Not state.IsKeyDown(Keys.LeftShift) AndAlso Not state.IsKeyDown(Keys.RightShift) Then selectionAnchor = SelectionHit(point)
            selectionEnd = SelectionHit(point)
        End Sub
        Private Sub DragTextSelection(point As Point, start As Point) Handles Me.MouseDrag
            If IsTextSelectionEnabled Then selectionEnd = SelectionHit(point)
        End Sub
        Private Sub TextSelectionKey(key As Keys, state As KeyboardState) Handles Me.OnKeyPress
            If Not IsTextSelectionEnabled Then Return
            If state.IsKeyDown(Keys.LeftControl) OrElse state.IsKeyDown(Keys.RightControl) Then
                If key = Keys.A Then
                    SelectAll()
                    Return
                End If
                If key = Keys.C OrElse key = Keys.Insert Then CopySelection()
            End If
            If key = Keys.Left OrElse key = Keys.Right OrElse key = Keys.Home OrElse key = Keys.End Then
                Dim extend = state.IsKeyDown(Keys.LeftShift) OrElse state.IsKeyDown(Keys.RightShift)
                Dim destination = selectionEnd
                Select Case key
                    Case Keys.Home : destination = 0
                    Case Keys.End : destination = Text.Length
                    Case Keys.Left : destination = If(Not extend AndAlso SelectionLength > 0, SelectionStart, Math.Max(0, selectionEnd - 1))
                    Case Keys.Right : destination = If(Not extend AndAlso SelectionLength > 0, SelectionStart + SelectionLength, Math.Min(Text.Length, selectionEnd + 1))
                End Select
                If destination > 0 AndAlso destination < Text.Length AndAlso Char.IsLowSurrogate(Text(destination)) Then destination += If(key = Keys.Left, -1, 1)
                selectionEnd = destination
                If Not extend Then selectionAnchor = destination
            End If
        End Sub
        Private Sub OpenTextSelectionMenu(point As Point) Handles Me.MouseRightClick
            If Not IsTextSelectionEnabled Then Return
            If selectionMenu Is Nothing Then
                selectionMenu = New ContextMenu(Scene) With {.Size = New Vector2(168, 72), .BackgroundColor = New Color(17, 23, 28)}
                For Each title In {"Copy", "Select all"}
                    Dim action = title
                    Dim item As New MenuItem(Scene) With {.Header = title, .AutoSize = ButtonAutoSize.None, .Size = New Vector2(160, 32), .ForegroundColor = Color.White}
                    AddHandler item.MouseLeftClick, Sub(p)
                                                        selectionMenu.IsOpen = False
                                                        If action = "Copy" Then CopySelection() Else SelectAll()
                                                        Scene.FocusElement(Me)
                                                    End Sub
                    selectionMenu.AddItem(item)
                Next
                Scene.AddElement(selectionMenu)
            End If
            Dim viewport = Scene.InputViewportSize
            selectionMenu.Position = New Vector2(Math.Clamp(Position.X + point.X, 0, Math.Max(0, viewport.X - 168)), Math.Clamp(Position.Y + point.Y, 0, Math.Max(0, viewport.Y - 72)))
            selectionMenu.IsOpen = True
        End Sub
        Private Sub DrawTextSelection(content As String)
            If Not IsTextSelectionEnabled OrElse SelectionLength = 0 Then Return
            If selectionTexture Is Nothing Then selectionTexture = Global.ProjectZ.Shared.Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White)
            For Each glyph In SelectionGlyphs(content)
                If glyph.Index < SelectionStart OrElse glyph.Index >= SelectionStart + SelectionLength Then Continue For
                Dim bounds = glyph.Bounds
                bounds.Offset(CInt(Position.X), CInt(Position.Y))
                bounds = Rectangle.Intersect(bounds, Me.Rectangle)
                If bounds.Width > 0 AndAlso bounds.Height > 0 Then spriteBatch.Draw(selectionTexture, bounds, ApplyOpacity(SelectionColor))
            Next
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                selectionTexture?.Dispose()
                If selectionMenu IsNot Nothing Then
                    Scene.RemoveElement(selectionMenu)
                    selectionMenu.Dispose()
                    selectionMenu = Nothing
                End If
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
