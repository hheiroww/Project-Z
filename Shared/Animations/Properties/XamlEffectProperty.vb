Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI

Namespace [Shared].Animations.Properties
    Public NotInheritable Class XamlEffectProperty
        Inherits ElementProperty
        Private ReadOnly surface As SceneElement
        Private ReadOnly name As String
        Public Sub New(target As SceneElement, propertyName As String)
            MyBase.New(target)
            surface = target
            name = propertyName.ToLowerInvariant()
            If Not {"radius", "blurradius", "amount", "phase", "angle", "opacity", "shadowdepth", "direction", "color"}.Contains(name) Then Throw New NotSupportedException("Effect property: " & propertyName)
        End Sub
        Public Overrides Function TargetsSameProperty(other As ElementProperty) As Boolean
            Dim typed = TryCast(other, XamlEffectProperty)
            Return typed IsNot Nothing AndAlso typed.name = name
        End Function
        Protected Friend Overrides Function GetValue() As Object
            Select Case name
                Case "radius", "blurradius" : Return surface.VisualEffect.Radius
                Case "amount" : Return surface.VisualEffect.Amount
                Case "phase" : Return surface.VisualEffect.Phase
                Case "angle" : Return surface.VisualEffect.Angle
                Case "opacity" : Return surface.VisualEffect.Opacity
                Case "shadowdepth" : Return surface.VisualEffect.ShadowDepth
                Case "direction" : Return surface.VisualEffect.Direction
                Case "color" : Return surface.VisualEffect.Color
            End Select
            Throw New NotSupportedException(name)
        End Function
        Protected Friend Overrides Sub SetValue(value As Object)
            Select Case name
                Case "radius", "blurradius" : surface.VisualEffect.Radius = CSng(value)
                Case "amount" : surface.VisualEffect.Amount = CSng(value)
                Case "phase" : surface.VisualEffect.Phase = CSng(value)
                Case "angle" : surface.VisualEffect.Angle = CSng(value)
                Case "opacity" : surface.VisualEffect.Opacity = CSng(value)
                Case "shadowdepth" : surface.VisualEffect.ShadowDepth = CSng(value)
                Case "direction" : surface.VisualEffect.Direction = CSng(value)
                Case "color" : surface.VisualEffect.Color = CType(value, Color)
            End Select
        End Sub
    End Class
End Namespace
