#Region "Using Statements"
Imports System.Collections.Generic
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced

Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Shared.XNA
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Animations.Easing

#End Region

Public Class DefaultScene
    Inherits Scene

    Public Sub New(ByRef SceneManager As SceneManager)
        MyBase.New(SceneManager)

        'Initialize Settings

        isCursorVisible = False

    End Sub

    Private Sub Me_Initialized(gameTime As GameTime) Handles Me.Initialized
        Dim TextElement1 As New TextElement(Me) With {
            .Text = "Please initialize the SceneManager Class." & Environment.NewLine &
                    "Instructions:" & Environment.NewLine &
                    "1. Find the LoadContent() method. ( zWindow.vb )" & Environment.NewLine &
                    "2. Define a Scene. EX: Dim Scene As New MainScene(sceneManager)" & Environment.NewLine &
                    "3. Add the scene to the scene manager. EX: SceneManager.AddScene(""MainScene"", Scene)" & Environment.NewLine &
                    "4. Set the scene to Active. EX: SceneManager.ActiveScene = Scene",
            .HorizontalAlign = HorizontalAlignment.Center,
            .VerticalAlign = VerticalAlignment.Center}

        AddElement(TextElement1)
        AddElement(TextElement1)
        AddElement(TextElement1)
    End Sub

End Class
