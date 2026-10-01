Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Animations.Easing
Imports ProjectZ.Shared.Animations.Properties
Imports ProjectZ.Shared.Content
Imports ProjectZ.Shared.Drawing.UI.Primitives


Namespace [Shared].Drawing.UI.Input

    <Serializable>
    Public Class Button
        Inherits RectangleElement

#Region "Properties"

        Public Property AutoSize As ButtonAutoSize
            Get
                Return _AutoSize
            End Get
            Set(value As ButtonAutoSize)
                _AutoSize = value
                If _AutoSize <> ButtonAutoSize.None Then
                    _OriginalSize = Size
                    DoAutoSize()
                Else
                    Size = _OriginalSize
                End If
            End Set
        End Property
        Private _AutoSize As ButtonAutoSize = ButtonAutoSize.XY

        ''' <summary>
        ''' Keeps an explicitly sized button on one line, expanding only when
        ''' its text would otherwise wrap or be clipped.
        ''' </summary>
        Public Property AutoSizeWidthOnTextOverflow As Boolean
            Get
                Return _AutoSizeWidthOnTextOverflow
            End Get
            Set(value As Boolean)
                _AutoSizeWidthOnTextOverflow = value
                If TextElement IsNot Nothing Then
                    TextElement.TextWrapping = If(value, TextWrapping.NoWrap, TextWrapping.Wrap)
                    Button_Changed()
                End If
            End Set
        End Property
        Private _AutoSizeWidthOnTextOverflow As Boolean
        Private _OriginalSize As Vector2

        Public Property isAnimated As Boolean = False

        Public Property Text As String
            Get
                Return _Text
            End Get
            Set(value As String)
                Dim oldText As String = _Text
                _Text = value
                RaiseEvent OnTextChanged(oldText, _Text)
                Button_Changed()
            End Set
        End Property
        Private _Text As String = String.Empty
        Public Property Font As String
            Get
                Return _Font
            End Get
            Set(value As String)
                _Font = value
                Button_Changed()
            End Set
        End Property
        Private _Font As String = Fonts.SegoeUI.GetResourceName(12)
        Public Property ForegroundColor As Color
            Get
                Return _ForegroundColor
            End Get
            Set(value As Color)
                _ForegroundColor = value
                If TextElement IsNot Nothing Then
                    TextElement.ForegroundColor = _ForegroundColor
                End If
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

        Public Property MouseOverBackgroundColor As Color
            Get
                Return _MouseOverBackgroundColor
            End Get
            Set(value As Color)
                _MouseOverBackgroundColor = value
            End Set
        End Property
        Private _MouseOverBackgroundColor As New Color(90, 90, 90)

        Public Property MouseDownBackgroundColor As Color
            Get
                Return _MouseDownBackgroundColor
            End Get
            Set(value As Color)
                _MouseDownBackgroundColor = value
            End Set
        End Property
        Private _MouseDownBackgroundColor As New Color(0, 150, 255)

#End Region

#Region "Events"

        Public Event OnTextChanged(oldText As String, newText As String)

#End Region

#Region "Animation Properties"

        Public Property MouseOverBackgroundProperty As New MouseOverBackgroundColorProperty(Me)

        Public Property MouseDownBackgroundProperty As New MouseDownBackgroundColorProperty(Me)
        Public Property MouseOverBackgroundBrush As XamlGradientBrush
        Public Property MouseDownBackgroundBrush As XamlGradientBrush

        Public Property ForegroundProperty As New ForegroundColorProperty(TextElement)

#Region "Animation Instances"
        Private MouseEnterAnimation As Animations.ColorAnimation
        Private MouseLeaveAnimation As Animations.ColorAnimation

        Private Sub OpenButton_OnMouseEnter() Handles Me.MouseEnter
            If isAnimated Then
                If MouseEnterAnimation Is Nothing Then
                    MouseEnterAnimation = New Animations.ColorAnimation(
                        New SineEase(EaseType.EaseInOut),
                        _BackgroundColor, _MouseOverBackgroundColor,
                        TimeSpan.FromSeconds(0.15), Scene.gameTime)
                    BindAnimation(MouseOverBackgroundProperty, MouseEnterAnimation)
                End If
                MouseEnterAnimation.Start()
            End If
        End Sub

        Private Sub OpenButton_OnMouseLeave() Handles Me.MouseLeave
            If isAnimated Then
                If MouseLeaveAnimation Is Nothing Then
                    MouseLeaveAnimation = New Animations.ColorAnimation(
                        New SineEase(EaseType.EaseInOut),
                        _MouseOverBackgroundColor, _BackgroundColor,
                        TimeSpan.FromSeconds(0.15), Scene.gameTime)
                    BindAnimation(BackgroundProperty, MouseLeaveAnimation)
                End If
                MouseLeaveAnimation.Start()
            End If
        End Sub

#End Region

#End Region

#Region "Child Elements"

        Protected Friend WithEvents TextElement As TextElement

        Private CanChange As Boolean = True
        Private Sub Button_Changed() Handles Me.RectangleChanged
            If CanChange AndAlso TextElement IsNot Nothing Then
                CanChange = False
                If AutoSize <> ButtonAutoSize.None Then DoAutoSize()
                TextElement.Font = Font
                TextElement.TextWrapping = If(AutoSizeWidthOnTextOverflow, TextWrapping.NoWrap, TextWrapping.Wrap)
                TextElement.Text = Text
                If AutoSizeWidthOnTextOverflow Then
                    Dim requiredWidth = Scene.MeasureText(Font, Text).X + Padding.Left + Padding.Right + 10
                    If requiredWidth > Size.X Then Size = New Vector2(requiredWidth, Size.Y)
                End If
                CanChange = True
            End If
        End Sub

        Private Sub DoAutoSize()
            If Scene Is Nothing Then Return
            Select Case AutoSize
                Case ButtonAutoSize.X
                    Size = New Vector2(Scene.MeasureText(Font, Text).X + Padding.Left + Padding.Right, Size.Y)
                Case ButtonAutoSize.Y
                    Size = New Vector2(Size.X, Scene.MeasureText(Font, Text).Y + Padding.Top + Padding.Bottom)
                Case ButtonAutoSize.XY
                    Size = Scene.MeasureText(Font, Text)
            End Select
        End Sub

#End Region

#Region "Constructors"

        Public Sub New(Scene As Scene)
            MyBase.New(Scene)
            Init()
        End Sub

        Public Sub New(Scene As Scene, spriteBatch As SpriteBatch)
            MyBase.New(Scene, spriteBatch)
            Init()
        End Sub

        Public Sub New(Scene As Scene, newSpritebatch As Boolean)
            MyBase.New(Scene, newSpritebatch)
            Init()
        End Sub

        Private Sub Init()
            CanSelect = True
            ' Create TextElement after base constructor has set Scene and spriteBatch
            TextElement = New TextElement(Scene, spriteBatch.SpriteBatch) With {
                .HorizontalAlign = HorizontalAlignment.Center,
                .VerticalAlign = VerticalAlignment.Center,
                .isMouseBypassEnabled = True,
                .ForegroundColor = _ForegroundColor,
                .TextWrapping = If(_AutoSizeWidthOnTextOverflow, TextWrapping.NoWrap, TextWrapping.Wrap)
            }
            Children.Add(TextElement)
            Clip = True
        End Sub

#End Region

        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            Dim DrawColor As Color

            If isMouseDown Then
                DrawColor = MouseDownBackgroundColor
            ElseIf isMouseOver OrElse isSelected Then
                DrawColor = MouseOverBackgroundColor
            Else
                DrawColor = BackgroundColor
            End If

            Dim stateBrush = If(isMouseDown, MouseDownBackgroundBrush, If(isMouseOver, MouseOverBackgroundBrush, BackgroundBrush))
            DrawBackground(DrawColor, Not isMouseDown AndAlso Not isMouseOver, stateBrush)
            ' The scene renders the registered text child after this background.
            ' Drawing it here as well doubles its coverage and nests its batch.
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                MouseOverBackgroundBrush?.Dispose()
                MouseDownBackgroundBrush?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

    End Class

    ' Temporary
    Public Enum ButtonAutoSize
        XY
        None
        X
        Y
    End Enum

End Namespace
