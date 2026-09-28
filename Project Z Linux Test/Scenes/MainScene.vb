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
Imports TriangleNet
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Animations.Easing
Imports System.Timers
Imports System.Reflection
Imports ProjectZ.Shared

#End Region

Public Class MainScene
    Inherits Scene


    Public Sub New(ByRef SceneManager As SceneManager)
        MyBase.New(SceneManager)

        'Initialize Settings

        isCursorVisible = True
        UseRenderTarget = True

        'SceneManager.DebugEnabled = True
    End Sub


End Class
