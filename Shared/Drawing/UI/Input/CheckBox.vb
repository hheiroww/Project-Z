Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Animations.Easing
Imports ProjectZ.Shared.Animations.Properties
Imports ProjectZ.Shared.Content
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Input

    ''' <summary>
    ''' A checkbox control similar to WPF CheckBox.
    ''' Provides IsChecked property with three-state support (checked, unchecked, indeterminate).
    ''' </summary>
    <Serializable>
    Public Class CheckBox
        Inherits RectangleElement

#Region "Properties"

        ''' <summary>
        ''' Gets or sets whether the checkbox is checked.
        ''' </summary>
        Public Property IsChecked As Boolean?
            Get
                Return _IsChecked
            End Get
            Set(value As Boolean?)
                Dim oldValue = _IsChecked
                _IsChecked = value
                If oldValue <> _IsChecked Then
                    UpdateCheckVisual()
                    RaiseEvent CheckedChanged(oldValue, _IsChecked)
                    If _IsChecked = True Then
                        RaiseEvent Checked()
                    ElseIf _IsChecked = False Then
                        RaiseEvent Unchecked()
                    Else
                        RaiseEvent Indeterminate()
                    End If
                End If
            End Set
        End Property
        Private _IsChecked As Boolean? = False

        ''' <summary>
        ''' Gets or sets whether the checkbox supports three states (checked, unchecked, indeterminate).
        ''' </summary>
        Public Property IsThreeState As Boolean = False

        ''' <summary>
        ''' Gets or sets the text content of the checkbox.
        ''' </summary>
        Public Property Content As String
            Get
                Return _Content
            End Get
            Set(value As String)
                _Content = value
                If ContentText IsNot Nothing Then
                    ContentText.Text = _Content
                    UpdateLayout()
                End If
            End Set
        End Property
        Private _Content As String = String.Empty

        ''' <summary>
        ''' Gets or sets the font for the content text.
        ''' </summary>
        Public Property Font As String
            Get
                Return _Font
            End Get
            Set(value As String)
                _Font = value
                If ContentText IsNot Nothing Then
                    ContentText.Font = _Font
                    UpdateLayout()
                End If
            End Set
        End Property
        Private _Font As String = Fonts.SegoeUI.GetResourceName(12)

        ''' <summary>
        ''' Gets or sets the foreground color.
        ''' </summary>
        Public Property ForegroundColor As Color
            Get
                Return _ForegroundColor
            End Get
            Set(value As Color)
                _ForegroundColor = value
                ContentText.ForegroundColor = _ForegroundColor
            End Set
        End Property
        Private _ForegroundColor As Color = Color.White

        ''' <summary>
        ''' Gets or sets the check mark color.
        ''' </summary>
        Public Property CheckColor As Color
            Get
                Return _CheckColor
            End Get
            Set(value As Color)
                _CheckColor = value
                CheckMark.BackgroundColor = _CheckColor
            End Set
        End Property
        Private _CheckColor As Color = Color.White

        ''' <summary>
        ''' Gets or sets the box border color.
        ''' </summary>
        Public Property BoxBorderColor As Color
            Get
                Return _BoxBorderColor
            End Get
            Set(value As Color)
                _BoxBorderColor = value
                CheckBoxBorder.BackgroundColor = _BoxBorderColor
            End Set
        End Property
        Private _BoxBorderColor As New Color(100, 100, 100)

        ''' <summary>
        ''' Gets or sets the box background color when checked.
        ''' </summary>
        Public Property CheckedBackgroundColor As Color
            Get
                Return _CheckedBackgroundColor
            End Get
            Set(value As Color)
                _CheckedBackgroundColor = value
                UpdateCheckVisual()
            End Set
        End Property
        Private _CheckedBackgroundColor As New Color(0, 120, 215)

        ''' <summary>
        ''' Size of the checkbox box.
        ''' </summary>
        Public Property BoxSize As Single = 18.0F

#End Region

#Region "Events"

        ''' <summary>
        ''' Raised when the IsChecked property changes.
        ''' </summary>
        Public Event CheckedChanged(oldValue As Boolean?, newValue As Boolean?)

        ''' <summary>
        ''' Raised when the checkbox becomes checked.
        ''' </summary>
        Public Event Checked()

        ''' <summary>
        ''' Raised when the checkbox becomes unchecked.
        ''' </summary>
        Public Event Unchecked()

        ''' <summary>
        ''' Raised when the checkbox becomes indeterminate.
        ''' </summary>
        Public Event Indeterminate()

#End Region

#Region "Child Elements"

        Private CheckBoxBorder As RectangleElement
        Private CheckBoxBackground As RectangleElement
        Private CheckMark As RectangleElement
        Private ContentText As TextElement

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
            BackgroundColor = Color.Transparent

            ' Create child elements after base constructor has set Scene and spriteBatch
            CheckBoxBorder = New RectangleElement(Scene, spriteBatch.SpriteBatch) With {
                .isMouseBypassEnabled = True,
                .BackgroundColor = _BoxBorderColor
            }

            CheckBoxBackground = New RectangleElement(Scene, spriteBatch.SpriteBatch) With {
                .isMouseBypassEnabled = True,
                .BackgroundColor = New Color(30, 30, 30)
            }

            CheckMark = New RectangleElement(Scene, spriteBatch.SpriteBatch) With {
                .isMouseBypassEnabled = True,
                .isVisible = False,
                .BackgroundColor = _CheckColor
            }

            ContentText = New TextElement(Scene, spriteBatch.SpriteBatch) With {
                .isMouseBypassEnabled = True,
                .VerticalAlign = VerticalAlignment.Center,
                .ForegroundColor = _ForegroundColor
            }

            Children.Add(CheckBoxBorder)
            Children.Add(CheckBoxBackground)
            Children.Add(CheckMark)
            Children.Add(ContentText)
            AddHandler Me.MouseLeftClick, AddressOf OnClick
            UpdateLayout()
        End Sub

#End Region

#Region "Methods"

        Private Sub OnClick(p As Point)
            If IsThreeState Then
                If _IsChecked Is Nothing Then
                    IsChecked = True
                ElseIf _IsChecked = True Then
                    IsChecked = False
                Else
                    IsChecked = Nothing
                End If
            Else
                IsChecked = Not _IsChecked.GetValueOrDefault(False)
            End If
        End Sub

        Private Sub UpdateLayout() Handles Me.RectangleChanged
            ' Skip if child elements are not yet initialized
            If CheckBoxBorder Is Nothing Then Return

            Dim borderThickness As Single = 2.0F

            CheckBoxBorder.Position = Position + New Vector2(0, (Size.Y - BoxSize) / 2.0F)
            CheckBoxBorder.Size = New Vector2(BoxSize, BoxSize)

            CheckBoxBackground.Position = Position + New Vector2(borderThickness, (Size.Y - BoxSize) / 2.0F + borderThickness)
            CheckBoxBackground.Size = New Vector2(BoxSize - borderThickness * 2, BoxSize - borderThickness * 2)

            Dim checkPadding As Single = 4.0F
            CheckMark.Position = Position + New Vector2(checkPadding, (Size.Y - BoxSize) / 2.0F + checkPadding)
            CheckMark.Size = New Vector2(BoxSize - checkPadding * 2, BoxSize - checkPadding * 2)

            ContentText.Position = Position + New Vector2(BoxSize + 8.0F, (Size.Y - ContentText.Size.Y) / 2.0F)
        End Sub

        Protected Overrides Sub AlignChildren()
            UpdateLayout()
        End Sub

        Private Sub UpdateCheckVisual()
            ' Skip if child elements are not yet initialized
            If CheckMark Is Nothing Then Return

            If _IsChecked = True Then
                CheckMark.isVisible = True
                CheckMark.BackgroundColor = _CheckColor
                CheckBoxBackground.BackgroundColor = _CheckedBackgroundColor
            ElseIf _IsChecked = False Then
                CheckMark.isVisible = False
                CheckBoxBackground.BackgroundColor = New Color(30, 30, 30)
            Else
                ' Indeterminate state - show smaller mark
                CheckMark.isVisible = True
                CheckMark.BackgroundColor = New Color(150, 150, 150)
                CheckBoxBackground.BackgroundColor = New Color(60, 60, 60)
            End If
        End Sub

        ''' <summary>
        ''' Toggles the checked state.
        ''' </summary>
        Public Sub Toggle()
            OnClick(Point.Zero)
        End Sub

#End Region

    End Class

End Namespace
