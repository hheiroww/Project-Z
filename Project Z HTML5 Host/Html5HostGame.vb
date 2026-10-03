Imports System.IO
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Html5

Friend NotInheritable Class Html5HostGame
    Inherits Game

    Private ReadOnly graphics As GraphicsDeviceManager
    Private host As Html5EngineHost
    Private smokeTask As Task(Of String)
    Private smokeReported As Boolean

    Public Sub New()
        graphics = New GraphicsDeviceManager(Me) With {
            .PreferredBackBufferWidth = 1280,
            .PreferredBackBufferHeight = 720,
            .SynchronizeWithVerticalRetrace = True
        }
        Window.Title = "Project Z | Embedded HTML5 Engine"
        Window.AllowUserResizing = True
        IsMouseVisible = True
    End Sub

    Private Sub Host_PageReady(sender As Object, e As EventArgs)
        If String.Equals(Environment.GetEnvironmentVariable("PROJECTZ_HTML5_SMOKE"), "1", StringComparison.Ordinal) Then
            smokeTask = host.ExecuteScriptAsync("JSON.stringify({ready:document.querySelector('#project-z-canvas')?.dataset.engineReady,motion:document.querySelector('#project-z-canvas')?.dataset.motion,url:location.href})")
        End If
    End Sub

    Private Sub Host_NavigationFailed(message As String)
        Console.Error.WriteLine($"PROJECTZ_HTML5_FAILED {message}")
        If String.Equals(Environment.GetEnvironmentVariable("PROJECTZ_HTML5_SMOKE"), "1", StringComparison.Ordinal) Then Me.Exit()
    End Sub

    Protected Overrides Sub Update(gameTime As GameTime)
        If host Is Nothing Then
            Try
                host = Html5EngineHost.Attach(Me, Path.Combine(AppContext.BaseDirectory, "Html5"))
                AddHandler host.PageReady, AddressOf Host_PageReady
                AddHandler host.NavigationFailed, AddressOf Host_NavigationFailed
            Catch ex As InvalidOperationException
                ' MonoGame Native publishes its SDL HWND after Initialize. The
                ' first game-loop frame is the earliest reliable attach point.
            End Try
        End If
        If smokeTask IsNot Nothing AndAlso smokeTask.IsCompleted AndAlso Not smokeReported Then
            smokeReported = True
            If smokeTask.IsCompletedSuccessfully AndAlso smokeTask.Result.Contains("project-z.local", StringComparison.Ordinal) Then
                Console.WriteLine($"PROJECTZ_HTML5_READY embedded=true server=false result={smokeTask.Result}")
            Else
                Console.Error.WriteLine("PROJECTZ_HTML5_FAILED page script did not complete")
            End If
            Me.Exit()
        End If
        MyBase.Update(gameTime)
    End Sub

    Protected Overrides Sub Draw(gameTime As GameTime)
        GraphicsDevice.Clear(New Color(6, 16, 14))
        MyBase.Draw(gameTime)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then host?.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
