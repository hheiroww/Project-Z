Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Content
Imports ProjectZ.Shared.Drawing

Public NotInheritable Class GameWindow
    Inherits Game

    Private ReadOnly graphics As GraphicsDeviceManager
    Private scenes As SceneManager
    Private batch As SpriteBatch

    Public Sub New()
        graphics = New GraphicsDeviceManager(Me) With {
            .PreferredBackBufferWidth = 960,
            .PreferredBackBufferHeight = 600,
            .GraphicsProfile = GraphicsProfile.HiDef,
            .SynchronizeWithVerticalRetrace = True
        }
        Content.RootDirectory = "Content"
        Window.Title = "$projectname$"
        Window.AllowUserResizing = True
        IsMouseVisible = True
    End Sub

    Protected Overrides Sub LoadContent()
        batch = New SpriteBatch(GraphicsDevice)
        Dim resources As New ContentContainer(Content, GraphicsDevice)
        resources.LoadAllContent()
        Services.AddService(GetType(SpriteBatch), batch)
        Services.AddService(GetType(GraphicsDevice), GraphicsDevice)
        Services.AddService(GetType(ContentContainer), resources)
        scenes = New SceneManager(Me, 60)
        Dim main As New MainScene(scenes)
        scenes.AddScene("Main", main)
        scenes.ActiveScene = main
    End Sub

    Protected Overrides Sub Update(gameTime As GameTime)
        If scenes IsNot Nothing Then scenes.Tick(gameTime)
        MyBase.Update(gameTime)
    End Sub

    Protected Overrides Sub Draw(gameTime As GameTime)
        If scenes IsNot Nothing Then scenes.Draw(gameTime)
        MyBase.Draw(gameTime)
    End Sub

    Protected Overrides Sub UnloadContent()
        If batch IsNot Nothing Then batch.Dispose()
    End Sub
End Class
