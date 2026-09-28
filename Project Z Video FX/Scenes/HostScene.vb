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
Imports ProjectZ.Shared.Audio

#End Region

Public Class HostScene
    Inherits Scene

    Public Property ChildScene As Scene
        Get
            Return _ChildScene
        End Get
        Set(value As Scene)
            _ChildScene = value
            If _ChildScene IsNot Nothing Then
                If HostElement Is Nothing Then
                    HostElement = New SceneProjectionHost(Me, _ChildScene)
                    AddElement(HostElement)
                Else
                    HostElement.TargetScene = _ChildScene
                End If
            End If
        End Set
    End Property
    Private _ChildScene As Scene

    Public Property HostElement As SceneProjectionHost

    Public Sub New(ByRef SceneManager As SceneManager, ChildScene As Scene)
        MyBase.New(SceneManager)

        'Initialize Settings

        isCursorVisible = True
        UseRenderTarget = True

        Me.ChildScene = ChildScene

    End Sub

    Private Sub Me_Initialized(gameTime As GameTime) Handles Me.Initialized
        HostElement.Scale = 2
    End Sub

#Region "Scale Test"

    Private Sub HostScene_PreDraw(gameTime As GameTime) Handles Me.PreDraw

    End Sub

#End Region
End Class
