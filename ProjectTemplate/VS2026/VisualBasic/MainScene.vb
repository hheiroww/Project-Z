Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.UI.Primitives

Public NotInheritable Class MainScene
    Inherits Scene

    Public Sub New(ByRef manager As SceneManager)
        MyBase.New(manager)
        BackgroundColor = New Color(18, 25, 35)
        EnableDesignTimeControls = False
        AddElement(New TextElement(Me) With {
            .Text = "Welcome to Project-Z",
            .Font = "SegoeUI_24",
            .Position = New Vector2(48, 56),
            .Size = New Vector2(820, 60)
        })
        AddElement(New TextElement(Me) With {
            .Text = "Renderer: " & GraphicsBackendInfo.Current.ToString() & " | Edit MainScene.vb to build your UI.",
            .Font = "SegoeUI_14",
            .Position = New Vector2(48, 130),
            .Size = New Vector2(850, 44)
        })
    End Sub
End Class
