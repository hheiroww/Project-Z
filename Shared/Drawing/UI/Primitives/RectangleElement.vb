Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Animations.Properties

Namespace [Shared].Drawing.UI.Primitives

    <Serializable>
    Public Class RectangleElement
        Inherits SceneElement

#Region "Properties"

        Protected Friend Texture As Texture2D
        Public Overridable Property BackgroundColor As New Color(60, 60, 60)
        Public Property BackgroundBrush As XamlGradientBrush
        Public Property CornerRadii As CornerRadii
        ''' <summary>Uniform-radius shorthand. Use CornerRadii for independent XAML corners.</summary>
        Public Property CornerRadius As Single
            Get
                Return CornerRadii.TopLeft
            End Get
            Set(value As Single)
                CornerRadii = New CornerRadii(value)
            End Set
        End Property
        Public Property BorderColor As Color = Color.Transparent
        Public Property BorderThickness As Single
        Private roundedBrush As XamlGradientBrush
        Public Overridable Property isResizable As AlignmentType = AlignmentType.None

        Protected Friend Property Color As Color
            Get
                Return _Color
            End Get
            Set(value As Color)
                _Color = value
                Try
                    Texture = Content.Textures.CreateSolidTexture(Scene.graphicsDevice, _Color)
                Catch ex As Exception
                End Try
            End Set
        End Property
        Private _Color As Color = Color.White

#End Region

#Region "Animation Properties"

        Friend BackgroundProperty As New BackgroundColorProperty(Me)

#End Region

#Region "Constructors"

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(Scene As Scene)
            MyBase.New(Scene)
            Try
                Texture = Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White)
            Catch ex As Exception

            End Try
        End Sub

        Public Sub New(Scene As Scene, spriteBatch As SpriteBatch)
            MyBase.New(Scene, spriteBatch)
            Texture = Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White)
        End Sub

        Public Sub New(Scene As Scene, newSpritebatch As Boolean)
            MyBase.New(Scene, newSpritebatch)
            Texture = Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White)
        End Sub

#End Region

        Protected Friend Overrides Sub Draw(gameTime As Microsoft.Xna.Framework.GameTime)
            DrawBackground(BackgroundColor)
        End Sub

        Protected Sub DrawBackground(fill As Color, Optional useBackgroundBrush As Boolean = True, Optional overrideBrush As XamlGradientBrush = Nothing)
            If Not CornerRadii.IsEmpty OrElse BorderThickness > 0 OrElse overrideBrush IsNot Nothing OrElse (useBackgroundBrush AndAlso BackgroundBrush IsNot Nothing) Then
                Dim brush = If(overrideBrush, If(useBackgroundBrush, BackgroundBrush, Nothing))
                If brush Is Nothing Then
                    If roundedBrush Is Nothing Then
                        roundedBrush = New XamlGradientBrush()
                        roundedBrush.Stops.Add(New XamlGradientStop())
                    End If
                    roundedBrush.Stops(0).Color = fill
                    brush = roundedBrush
                End If
                ' Generate in layout units, then transform the entire surface,
                ' including its corners and stroke, just as WPF does.
                spriteBatch.Draw(brush.GetTexture(Scene.graphicsDevice, CInt(Size.X), CInt(Size.Y), CornerRadii, BorderThickness, BorderColor), Rectangle, ApplyOpacity(Color.White))
                DrawFocusIndicator()
                Return
            End If
            If Texture Is Nothing Then Texture = Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White)
            spriteBatch.Draw(Texture, Rectangle, ApplyOpacity(fill))
            DrawFocusIndicator()
        End Sub

        Private Sub DrawFocusIndicator()
            If Not isSelected OrElse Not CanSelect Then Return
            If Texture Is Nothing Then Texture = Content.Textures.CreateSolidTexture(Scene.graphicsDevice, Color.White)
            Dim r = Rectangle
            Dim focusColor = ApplyOpacity(New Color(130, 190, 255))
            spriteBatch.Draw(Texture, New Rectangle(r.X, r.Y, r.Width, 2), focusColor)
            spriteBatch.Draw(Texture, New Rectangle(r.X, r.Bottom - 2, r.Width, 2), focusColor)
            spriteBatch.Draw(Texture, New Rectangle(r.X, r.Y, 2, r.Height), focusColor)
            spriteBatch.Draw(Texture, New Rectangle(r.Right - 2, r.Y, 2, r.Height), focusColor)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                BackgroundBrush?.Dispose()
                roundedBrush?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

        Private Sub RectangleElement_MouseMove(currentPoint As Point, lastPoint As Point) Handles Me.MouseMove
            alignment(currentPoint, lastPoint)
        End Sub

        Private Sub alignment(currentPoint As Point, lastPoint As Point)
            If isResizable = AlignmentType.Horizontal Then
                If currentPoint.X >= Size.X - 4 Then
                    EnableResizeCursor(AlignmentType.Horizontal, 1)
                ElseIf currentPoint.X <= 4 Then
                    EnableResizeCursor(AlignmentType.Horizontal, -1)
                Else
                    Scene.ChangeCursorType(CursorType.Default)
                End If
            ElseIf isResizable = AlignmentType.Vertical Then
                If currentPoint.Y >= Size.Y - 4 Then
                    EnableResizeCursor(AlignmentType.Vertical, 1)
                ElseIf currentPoint.Y <= 4 Then
                    EnableResizeCursor(AlignmentType.Vertical, -1)
                End If
            Else
                Scene.ChangeCursorType(CursorType.Default)
            End If
        End Sub

        Private Sub EnableResizeCursor(type As AlignmentType, location As Integer)
            If location < 0 AndAlso type = AlignmentType.Horizontal Then
                Scene.ChangeCursorType(CursorType.ResizeLeft)

            ElseIf location < 0 AndAlso type = AlignmentType.Vertical Then
                Scene.ChangeCursorType(CursorType.ResizeTop)
            End If
            If location > 0 AndAlso type = AlignmentType.Horizontal Then
                Scene.ChangeCursorType(CursorType.ResizeRight)
            ElseIf location > 0 AndAlso type = AlignmentType.Vertical Then
                Scene.ChangeCursorType(CursorType.ResizeBottom)
            End If
        End Sub

        Private Sub RectangleElement_Loaded() Handles Me.Loaded

        End Sub
    End Class

End Namespace
