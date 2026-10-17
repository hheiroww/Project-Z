Imports Microsoft.Xna.Framework

Namespace [Shared].Drawing.UI.Input
    ''' <summary>A titled, collapsible group with a quiet header rather than a raised button.</summary>
    Public Class CollapsibleGroupBox
        Inherits Expander
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = Color.Transparent
            BorderColor = New Color(34, 70, 57)
            BorderThickness = 1
            CornerRadius = 4
            HeaderElement.BackgroundColor = Color.Transparent
            HeaderElement.MouseOverBackgroundColor = New Color(15, 52, 39)
            HeaderElement.BorderThickness = 0
            HeaderElement.Font = "SegoeUI_10"
        End Sub
    End Class
End Namespace
