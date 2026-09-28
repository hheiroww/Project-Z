Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Content
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports System.Collections.Generic

Namespace [Shared].Drawing.UI.Input

    ''' <summary>
    ''' A radio button control similar to WPF RadioButton.
    ''' Only one RadioButton in a group can be selected at a time.
    ''' </summary>
    <Serializable>
    Public Class RadioButton
        Inherits RectangleElement

#Region "Shared State"

        ''' <summary>
        ''' Dictionary tracking radio button groups.
        ''' </summary>
        Private Shared ReadOnly Groups As New Dictionary(Of String, List(Of RadioButton))

#End Region

#Region "Properties"

        ''' <summary>
        ''' Gets or sets whether this radio button is checked.
        ''' </summary>
        Public Property IsChecked As Boolean
            Get
                Return _IsChecked
            End Get
            Set(value As Boolean)
                If _IsChecked <> value Then
                    If value Then
                        ' Uncheck others in the same group
                        UnselectOthersInGroup()
                    End If
                    _IsChecked = value
                    UpdateCheckVisual()
                    RaiseEvent CheckedChanged(_IsChecked)
                    If _IsChecked Then
                        RaiseEvent Checked()
                    Else
                        RaiseEvent Unchecked()
                    End If
                End If
            End Set
        End Property
        Private _IsChecked As Boolean = False

        ''' <summary>
        ''' Gets or sets the group name. Radio buttons with the same group name are mutually exclusive.
        ''' </summary>
        Public Property GroupName As String
            Get
                Return _GroupName
            End Get
            Set(value As String)
                ' Remove from old group
                RemoveFromGroup()
                _GroupName = If(value, String.Empty)
                ' Add to new group
                AddToGroup()
            End Set
        End Property
        Private _GroupName As String = String.Empty

        ''' <summary>
        ''' Gets or sets the text content.
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
        ''' Gets or sets the font.
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
                If ContentText IsNot Nothing Then
                    ContentText.ForegroundColor = _ForegroundColor
                End If
            End Set
        End Property
        Private _ForegroundColor As Color = Color.White

        ''' <summary>
        ''' Gets or sets the selected indicator color.
        ''' </summary>
        Public Property SelectedColor As Color
            Get
                Return _SelectedColor
            End Get
            Set(value As Color)
                _SelectedColor = value
                UpdateCheckVisual()
            End Set
        End Property
        Private _SelectedColor As New Color(0, 120, 215)

        ''' <summary>
        ''' Gets or sets the border color.
        ''' </summary>
        Public Shadows Property BorderColor As Color
            Get
                Return _BorderColor
            End Get
            Set(value As Color)
                _BorderColor = value
                OuterCircle.FillColor = _BorderColor
            End Set
        End Property
        Private _BorderColor As New Color(100, 100, 100)

        ''' <summary>
        ''' Size of the radio button circle.
        ''' </summary>
        Public Property CircleSize As Single = 18.0F

#End Region

#Region "Events"

        ''' <summary>
        ''' Raised when IsChecked changes.
        ''' </summary>
        Public Event CheckedChanged(isChecked As Boolean)

        ''' <summary>
        ''' Raised when this radio button becomes checked.
        ''' </summary>
        Public Event Checked()

        ''' <summary>
        ''' Raised when this radio button becomes unchecked.
        ''' </summary>
        Public Event Unchecked()

#End Region

#Region "Child Elements"

        Private OuterCircle As CircleElement
        Private InnerCircle As CircleElement
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
            OuterCircle = New CircleElement(Scene, spriteBatch.SpriteBatch) With {
                .isMouseBypassEnabled = True,
                .FillColor = _BorderColor
            }

            InnerCircle = New CircleElement(Scene, spriteBatch.SpriteBatch) With {
                .isMouseBypassEnabled = True,
                .isVisible = False
            }

            ContentText = New TextElement(Scene, spriteBatch.SpriteBatch) With {
                .isMouseBypassEnabled = True,
                .VerticalAlign = VerticalAlignment.Center,
                .ForegroundColor = _ForegroundColor
            }

            Children.Add(OuterCircle)
            Children.Add(InnerCircle)
            Children.Add(ContentText)
            AddHandler Me.MouseLeftClick, AddressOf OnClick
            AddToGroup()
            UpdateLayout()
        End Sub

#End Region

#Region "Methods"

        Private Sub OnClick(p As Point)
            If Not _IsChecked Then
                IsChecked = True
            End If
        End Sub

        Private Sub AddToGroup()
            If String.IsNullOrEmpty(_GroupName) Then Return
            SyncLock Groups
                If Not Groups.ContainsKey(_GroupName) Then
                    Groups(_GroupName) = New List(Of RadioButton)
                End If
                If Not Groups(_GroupName).Contains(Me) Then
                    Groups(_GroupName).Add(Me)
                End If
            End SyncLock
        End Sub

        Private Sub RemoveFromGroup()
            If String.IsNullOrEmpty(_GroupName) Then Return
            SyncLock Groups
                If Groups.ContainsKey(_GroupName) Then
                    Groups(_GroupName).Remove(Me)
                    If Groups(_GroupName).Count = 0 Then
                        Groups.Remove(_GroupName)
                    End If
                End If
            End SyncLock
        End Sub

        Private Sub UnselectOthersInGroup()
            If String.IsNullOrEmpty(_GroupName) Then Return
            SyncLock Groups
                If Groups.ContainsKey(_GroupName) Then
                    For Each radioButton In Groups(_GroupName)
                        If radioButton IsNot Me AndAlso radioButton._IsChecked Then
                            radioButton._IsChecked = False
                            radioButton.UpdateCheckVisual()
                            radioButton.RaiseEvent_Unchecked()
                        End If
                    Next
                End If
            End SyncLock
        End Sub

        Private Sub RaiseEvent_Unchecked()
            RaiseEvent CheckedChanged(False)
            RaiseEvent Unchecked()
        End Sub

        Private Sub UpdateLayout() Handles Me.RectangleChanged
            ' Skip if child elements are not yet initialized
            If OuterCircle Is Nothing Then Return

            Dim yCenter As Single = (Size.Y - CircleSize) / 2.0F

            OuterCircle.Position = New Vector2(0, yCenter)
            OuterCircle.Size = New Vector2(CircleSize, CircleSize)

            Dim innerSize As Single = CircleSize * 0.5F
            Dim innerOffset As Single = (CircleSize - innerSize) / 2.0F
            InnerCircle.Position = New Vector2(innerOffset, yCenter + innerOffset)
            InnerCircle.Size = New Vector2(innerSize, innerSize)

            ContentText.Position = New Vector2(CircleSize + 8.0F, 0)
        End Sub

        Private Sub UpdateCheckVisual()
            ' Skip if child elements are not yet initialized
            If InnerCircle Is Nothing Then Return

            If _IsChecked Then
                InnerCircle.isVisible = True
                InnerCircle.FillColor = _SelectedColor
                OuterCircle.FillColor = _SelectedColor
            Else
                InnerCircle.isVisible = False
                OuterCircle.FillColor = _BorderColor
            End If
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                RemoveFromGroup()
            End If
            MyBase.Dispose(disposing)
        End Sub

#End Region

    End Class

End Namespace
