Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.Designer
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition
Imports System.IO

Public Class TetrisViewport
    Inherits Scene

    Private ReadOnly game As TetrisElement
    Private ReadOnly header As RectangleElement
    Private ReadOnly sidebar As RectangleElement
    Private ReadOnly title As TextElement
    Private ReadOnly workspaceLabel As TextElement
    Private ReadOnly heading As TextElement
    Private ReadOnly effectStatus As TextElement
    Private ReadOnly scoreLabel As TextElement
    Private ReadOnly moveHelp As TextElement
    Private ReadOnly dropHelp As TextElement
    Private ReadOnly themeButton As Button
    Private ReadOnly editorButton As Button
    Private ReadOnly resetButton As Button
    Private ReadOnly minimizeButton As Button
    Private ReadOnly closeButton As Button
    Private editor As Global.ProjectZ.ShaderEditorSession
    Private slateTheme As Boolean

    Public Sub New(manager As SceneManager)
        MyBase.New(manager)
        isCursorVisible = False

        Dim parser As New SceneXamlParser(Me)
        Using stream = GetType(TetrisViewport).Assembly.GetManifestResourceStream("ProjectZ_Application.TetrisWorkspace.xaml")
            If stream Is Nothing Then Throw New InvalidDataException("The Tetris workspace XAML resource is missing.")
            Using reader As New StreamReader(stream)
                AddElement(parser.Parse(reader.ReadToEnd()))
            End Using
        End Using
        header = parser.FindName(Of RectangleElement)("Header")
        sidebar = parser.FindName(Of RectangleElement)("Sidebar")
        title = parser.FindName(Of TextElement)("AppTitle")
        workspaceLabel = parser.FindName(Of TextElement)("WorkspaceLabel")
        heading = parser.FindName(Of TextElement)("Heading")
        effectStatus = parser.FindName(Of TextElement)("EffectStatus")
        scoreLabel = parser.FindName(Of TextElement)("ScoreLabel")
        moveHelp = parser.FindName(Of TextElement)("MoveHelp")
        dropHelp = parser.FindName(Of TextElement)("DropHelp")

        editorButton = parser.FindName(Of Button)("OpenEditor")
        resetButton = parser.FindName(Of Button)("ResetEffect")
        themeButton = parser.FindName(Of Button)("Theme")
        minimizeButton = parser.FindName(Of Button)("Minimize")
        closeButton = parser.FindName(Of Button)("Close")
        Dim window = DirectCast(sender, zWindow)
        AddHandler editorButton.MouseLeftClick, Sub(point) OpenEditor()
        AddHandler resetButton.MouseLeftClick, Sub(point) ResetEffect()
        AddHandler themeButton.MouseLeftClick, Sub(point) SwitchTheme()
        AddHandler minimizeButton.MouseLeftClick, Sub(point) window.Minimize()
        AddHandler closeButton.MouseLeftClick, Sub(point) window.CloseWindow()
        game = New TetrisElement(Me, 12, 24, 23) With {.Position = New Vector2(421, 71)}
        AddElement(game)

        SetTheme()
    End Sub

    Private Sub SwitchTheme()
        slateTheme = Not slateTheme
        SetTheme()
    End Sub

    Private Sub SetTheme()
        Dim settings = WindowAppearance.Default.Settings
        settings.BackgroundColor = If(slateTheme, New Color(16, 21, 34), New Color(6, 21, 17))
        settings.SurfaceColor = If(slateTheme, New Color(30, 41, 65), New Color(10, 44, 34))
        settings.TextColor = If(slateTheme, New Color(230, 237, 254), New Color(227, 245, 236))
        settings.AccentColor = If(slateTheme, New Color(138, 179, 255), New Color(113, 235, 151))
        WindowAppearance.Default.Update(settings)
        themeButton.Text = If(slateTheme, "Theme  ·  Slate", "Theme  ·  Forest")
    End Sub

    Friend Sub ApplyAppearance(settings As WindowAppearanceSettings)
        BackgroundColor = settings.Background(settings.BackgroundColor)
        header.BackgroundColor = settings.SurfaceColor
        sidebar.BackgroundColor = settings.SurfaceColor * 0.72F
        sidebar.BorderThickness = 1
        sidebar.BorderColor = Color.Lerp(settings.SurfaceColor, settings.AccentColor, 0.3F)
        title.ForegroundColor = settings.TextColor
        workspaceLabel.ForegroundColor = settings.AccentColor
        heading.ForegroundColor = settings.TextColor
        effectStatus.ForegroundColor = Color.Lerp(settings.TextColor, settings.SurfaceColor, 0.18F)
        scoreLabel.ForegroundColor = settings.TextColor
        moveHelp.ForegroundColor = effectStatus.ForegroundColor
        dropHelp.ForegroundColor = effectStatus.ForegroundColor
        For Each button In {editorButton, resetButton, themeButton, minimizeButton, closeButton}
            button.BackgroundColor = settings.SurfaceColor
            button.ForegroundColor = settings.TextColor
            button.MouseOverBackgroundColor = Color.Lerp(settings.SurfaceColor, settings.AccentColor, 0.16F)
            button.MouseDownBackgroundColor = Color.Lerp(settings.SurfaceColor, settings.AccentColor, 0.32F)
        Next
        editorButton.ForegroundColor = settings.AccentColor
        game.BoardColor = Color.Lerp(settings.BackgroundColor, settings.SurfaceColor, 0.45F)
        game.BorderColor = Color.Lerp(settings.SurfaceColor, settings.AccentColor, 0.4F)
        game.GridLineColor = Color.Lerp(game.BoardColor, game.BorderColor, 0.2F)
        game.LabelColor = settings.TextColor
        game.LabelAccentColor = settings.AccentColor
        RequestRender()
    End Sub

    Private Sub OpenEditor()
        If editor IsNot Nothing AndAlso Not editor.Completion.IsCompleted Then Return
        Try
            editor = Global.ProjectZ.Shaders.ShowEditor()
            effectStatus.Text = "Run a shader to apply it to this board."
        Catch errorInfo As Exception
            Debug.WriteLine(errorInfo)
            effectStatus.Text = "The shader editor could not open."
        End Try
    End Sub

    Private Sub ResetEffect()
        Dim previous = game.VisualEffect
        game.VisualEffect = Nothing
        previous?.Dispose()
        effectStatus.Text = "Board effect cleared."
        RequestRender()
    End Sub

    Public Overrides Sub Tick(gameTime As GameTime)
        MyBase.Tick(gameTime)
        Dim score = If(game.IsGameOver, "Game over  ·  R restarts", $"Score {game.Score}  ·  Lines {game.LinesCleared}")
        If scoreLabel.Text <> score Then scoreLabel.Text = score
        Dim effect As XamlNativeEffect = Nothing
        If editor IsNot Nothing AndAlso editor.TryTakeEffect(effect) Then
            Dim previous = game.VisualEffect
            game.VisualEffect = effect
            previous?.Dispose()
            effectStatus.Text = "Shader applied to the Tetris board."
            RequestRender()
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then editor?.Close()
        MyBase.Dispose(disposing)
    End Sub
End Class
