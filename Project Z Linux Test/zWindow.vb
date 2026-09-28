#Region "Using Statements"
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Content

#End Region

Public Class zWindow
    Inherits Game

#Region "Properties"

    Public Property Size As New Point(800, 600)

#End Region

#Region "Internals"

    Public graphics As GraphicsDeviceManager
    Public spriteBatch As SpriteBatch
    Public contentCollection As ContentContainer
    Public sceneManager As SceneManager

#End Region

#Region "Drawing"

    Protected Overrides Sub Draw(gameTime As GameTime)

        ' Draw the current Scene
        sceneManager.Draw(gameTime)

        MyBase.Draw(gameTime)
    End Sub

#End Region

    Public Sub New()
        MyBase.New()

        graphics = New GraphicsDeviceManager(Me) With {
            .PreferMultiSampling = True,
            .SynchronizeWithVerticalRetrace = True,
            .PreferredBackBufferHeight = Size.Y,
            .PreferredBackBufferWidth = Size.X,
            .GraphicsProfile = GraphicsProfile.HiDef}

        IsFixedTimeStep = True
        Content.RootDirectory = "Content"
        IsMouseVisible = False
        Window.AllowUserResizing = True
    End Sub

    Protected Overrides Sub Update(gameTime As GameTime)
        MyBase.Update(gameTime)
        sceneManager.Tick(gameTime)
    End Sub

    Protected Overrides Sub Initialize()
        MyBase.Initialize()
    End Sub

    Protected Overrides Sub LoadContent()

        ' Create the Sprite Batch
        spriteBatch = New SpriteBatch(GraphicsDevice)

        ' Initialize the Content Container
        contentCollection = New ContentContainer(Content, GraphicsDevice)

        ' Load Content
        contentCollection.LoadAllContent()

        ' Add our services to access them from another class instance
        Services.AddService(GetType(SpriteBatch), spriteBatch)
        Services.AddService(GetType(GraphicsDevice), GraphicsDevice)
        Services.AddService(GetType(ContentContainer), contentCollection)

        ' Initialize the Scene Manager
        sceneManager = New SceneManager(Me)

        Dim TestScene As New MainScene(sceneManager)
        sceneManager.AddScene("TestScene", TestScene)
        sceneManager.ActiveScene = TestScene
        'Create the Scene
        'Dim S As New HostScene(sceneManager, New MainScene(sceneManager))
        'sceneManager.AddScene("Root", S)
        'sceneManager.ActiveScene = S

    End Sub

    Protected Overrides Sub UnloadContent()

    End Sub

End Class