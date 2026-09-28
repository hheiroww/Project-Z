Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Animations.Properties

#Region "Property Classes"

    Public Class GradientStopProperty
        Inherits ElementProperty
        Private ReadOnly stopValue As XamlGradientStop
        Private ReadOnly isColor As Boolean
        Public Sub New(target As SceneElement, gradientStop As XamlGradientStop, color As Boolean)
            MyBase.New(target)
            stopValue = gradientStop
            isColor = color
        End Sub
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim other = TryCast(obj, GradientStopProperty)
            Return other IsNot Nothing AndAlso Object.ReferenceEquals(stopValue, other.stopValue) AndAlso isColor = other.isColor
        End Function
        Public Overrides Function GetHashCode() As Integer
            Return HashCode.Combine(stopValue, isColor)
        End Function
        Public Overrides Function TargetsSameProperty(other As ElementProperty) As Boolean
            Return Equals(other)
        End Function
        Protected Friend Overrides Function GetValue() As Object
            If isColor Then Return stopValue.Color
            Return stopValue.Offset
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            If isColor Then
                stopValue.Color = CType(value, Color)
            Else
                stopValue.Offset = CSng(value)
            End If
        End Sub
    End Class

    Public Class WidthProperty
        Inherits ElementProperty

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(TargetElement As SceneElement)
            MyBase.New(TargetElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.Size.X
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            TargetElement.Size = New Vector2(CSng(Value), CInt(TargetElement.Size.Y))
        End Sub
    End Class
    Public Class HeightProperty
        Inherits ElementProperty

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(TargetElement As SceneElement)
            MyBase.New(TargetElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.Size.Y
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            TargetElement.Size = New Vector2(CInt(TargetElement.Size.X), CSng(Value))
        End Sub
    End Class

    Public Class OpacityProperty
        Inherits ElementProperty

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(TargetElement As SceneElement)
            MyBase.New(TargetElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.Opacity
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            TargetElement.Opacity = Math.Clamp(CSng(Value), 0.0F, 1.0F)
        End Sub
    End Class

    Public Class LeftProperty
        Inherits ElementProperty

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(TargetElement As SceneElement)
            MyBase.New(TargetElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.Position.X
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            TargetElement.Position = New Vector2(CSng(Value), CInt(TargetElement.Position.Y))
        End Sub
    End Class
    Public Class TopProperty
        Inherits ElementProperty

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByRef TargetElement As SceneElement)
            MyBase.New(TargetElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.Position.Y
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            TargetElement.Position = New Vector2(TargetElement.Position.X, CSng(Value))
        End Sub
    End Class

    Public Class ScaleXProperty
        Inherits ElementProperty
        Public Sub New(target As SceneElement)
            MyBase.New(target)
        End Sub
        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.RenderScale.X
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            TargetElement.RenderScale = New Vector2(CSng(value), TargetElement.RenderScale.Y)
        End Sub
    End Class

    Public Class ScaleYProperty
        Inherits ElementProperty
        Public Sub New(target As SceneElement)
            MyBase.New(target)
        End Sub
        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.RenderScale.Y
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            TargetElement.RenderScale = New Vector2(TargetElement.RenderScale.X, CSng(value))
        End Sub
    End Class

    Public Class TranslationXProperty
        Inherits ElementProperty
        Public Sub New(target As SceneElement)
            MyBase.New(target)
        End Sub
        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.RenderTranslation.X
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            TargetElement.RenderTranslation = New Vector2(CSng(value), TargetElement.RenderTranslation.Y)
        End Sub
    End Class

    Public Class TranslationYProperty
        Inherits ElementProperty
        Public Sub New(target As SceneElement)
            MyBase.New(target)
        End Sub
        Protected Friend Overrides Function GetValue() As Object
            Return TargetElement.RenderTranslation.Y
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            TargetElement.RenderTranslation = New Vector2(TargetElement.RenderTranslation.X, CSng(value))
        End Sub
    End Class

    Public Class BorderColorProperty
        Inherits ElementProperty
        Private ReadOnly border As Border
        Public Sub New(target As Border)
            MyBase.New(target)
            border = target
        End Sub
        Protected Friend Overrides Function GetValue() As Object
            Return border.BorderColor
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            border.BorderColor = CType(value, Color)
        End Sub
    End Class

    Public Class CornerRadiusProperty
        Inherits ElementProperty
        Private ReadOnly border As RectangleElement
        Public Sub New(target As RectangleElement)
            MyBase.New(target)
            border = target
        End Sub
        Protected Friend Overrides Function GetValue() As Object
            Return border.CornerRadius
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            border.CornerRadius = CSng(value)
        End Sub
        Public Overrides Function TargetsSameProperty(other As ElementProperty) As Boolean
            Return TypeOf other Is CornerRadiusProperty OrElse TypeOf other Is CornerRadiiProperty
        End Function
    End Class

    Public Class CornerRadiiProperty
        Inherits ElementProperty
        Private ReadOnly surface As RectangleElement
        Public Sub New(target As RectangleElement)
            MyBase.New(target)
            surface = target
        End Sub
        Protected Friend Overrides Function GetValue() As Object
            Return surface.CornerRadii
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            surface.CornerRadii = CType(value, CornerRadii)
        End Sub
        Public Overrides Function TargetsSameProperty(other As ElementProperty) As Boolean
            Return TypeOf other Is CornerRadiiProperty OrElse TypeOf other Is CornerRadiusProperty
        End Function
    End Class

    Public Class BackgroundColorProperty
        Inherits ElementProperty

        Private CastedElement As RectangleElement

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByRef TargetElement As SceneElement)
            MyBase.New(TargetElement)
            CastedElement = DirectCast(TargetElement, RectangleElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return CastedElement.BackgroundColor
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            CastedElement.BackgroundColor = CType(Value, Color)
        End Sub
    End Class
    Public Class ForegroundColorProperty
        Inherits ElementProperty

        Private CastedElement As TextElement

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByRef TargetElement As TextElement)
            MyBase.New(TargetElement)
            CastedElement = CType(TargetElement, TextElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return CastedElement.ForegroundColor
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            CastedElement.ForegroundColor = CType(Value, Color)
        End Sub
    End Class
    Public Class FillColorProperty
        Inherits ElementProperty

        Private CastedElement As PolygonElement

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByRef TargetElement As SceneElement)
            MyBase.New(TargetElement)
            CastedElement = DirectCast(TargetElement, PolygonElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return CastedElement.FillColor
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            CastedElement.FillColor = CType(Value, Color)
        End Sub
    End Class

    Public Class MouseOverBackgroundColorProperty
        Inherits ElementProperty

        Private CastedElement As Button

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByRef TargetElement As SceneElement)
            MyBase.New(TargetElement)
            CastedElement = DirectCast(TargetElement, Button)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return CastedElement.MouseOverBackgroundColor
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            CastedElement.MouseOverBackgroundColor = CType(Value, Color)
        End Sub
    End Class
    Public Class MouseDownBackgroundColorProperty
        Inherits ElementProperty

        Private CastedElement As Button

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByRef TargetElement As SceneElement)
            MyBase.New(TargetElement)
            CastedElement = DirectCast(TargetElement, Button)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return CastedElement.MouseDownBackgroundColor
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            CastedElement.MouseDownBackgroundColor = CType(Value, Color)
        End Sub
    End Class

    Public Class SpriteProgressProperty
        Inherits ElementProperty

        Private CastedElement As SpriteElement

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(TargetElement As SceneElement)
            MyBase.New(TargetElement)
            CastedElement = DirectCast(TargetElement, SpriteElement)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return CastedElement.CurrentFrame
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            CastedElement.CurrentFrame = CInt(Value)
        End Sub
    End Class

    Public Class TrackbarValueProperty
        Inherits ElementProperty

        Private CastedElement As Trackbar

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(TargetElement As SceneElement)
            MyBase.New(TargetElement)
            CastedElement = DirectCast(TargetElement, Trackbar)
        End Sub

        Protected Friend Overrides Function GetValue() As Object
            Return CastedElement.Value
        End Function

        Protected Friend Overrides Sub SetValue(Value As Object)
            CastedElement.Value = CDbl(Value)
        End Sub
    End Class

#End Region

    Public MustInherit Class ElementProperty

        Public Overridable Function TargetsSameProperty(other As ElementProperty) As Boolean
            Return other IsNot Nothing AndAlso Me.GetType() Is other.GetType()
        End Function

        Friend TargetElement As SceneElement

        Protected Friend MustOverride Function GetValue() As Object

        Protected Friend MustOverride Sub SetValue(Value As Object)

        Public Sub New(TargetElement As SceneElement)
            Me.TargetElement = TargetElement
        End Sub

        Public Sub New()
        End Sub

    End Class

End Namespace
