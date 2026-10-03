Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Diagnostics
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms
Imports Microsoft.Xna.Framework
Imports Forms = System.Windows.Forms

Namespace [Shared].Html5

    ''' <summary>
    ''' Hosts an HTML5 application inside the HWND owned by a running Project-Z
    ''' game. Content is mapped directly from disk through WebView2; no HTTP
    ''' server or external browser process is required.
    ''' </summary>
    Public NotInheritable Class Html5EngineHost
        Implements IDisposable

        Private Const VirtualHost As String = "project-z.local"
        Private Const SWP_NOACTIVATE As UInteger = &H10UI
        Private Const SWP_NOZORDER As UInteger = &H4UI

        Private ReadOnly _game As Game
        Private ReadOnly _contentRoot As String
        Private ReadOnly _container As NativeChildHost
        Private ReadOnly _browser As WebView2
        Private _disposed As Boolean

        Public Event PageReady As EventHandler
        Public Event NavigationFailed(message As String)

        Public ReadOnly Property IsReady As Boolean
        Public ReadOnly Property Source As Uri
            Get
                Return _browser.Source
            End Get
        End Property

        Private Sub New(game As Game, contentRoot As String)
            If game Is Nothing Then Throw New ArgumentNullException(NameOf(game))
            Dim parentWindow = ResolveNativeWindow(game)
            If parentWindow = IntPtr.Zero Then Throw New InvalidOperationException("Project-Z must create its native window before attaching WebView2.")

            _game = game
            _contentRoot = Path.GetFullPath(contentRoot)
            If Not File.Exists(Path.Combine(_contentRoot, "index.html")) Then
                Throw New DirectoryNotFoundException($"HTML5 content was not found at '{_contentRoot}'.")
            End If

            _container = New NativeChildHost(parentWindow) With {
                .BackColor = Global.System.Drawing.Color.FromArgb(6, 16, 14)
            }
            _browser = New WebView2 With {
                .BackColor = Global.System.Drawing.Color.FromArgb(6, 16, 14),
                .Dock = Forms.DockStyle.Fill,
                .CreationProperties = New CoreWebView2CreationProperties With {
                    .UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectZ", "WebView2")
                }
            }
            _container.Controls.Add(_browser)
            _container.CreateControl()
            _browser.CreateControl()
            ResizeToWindow()
            _container.Visible = True
            _container.BringToFront()

            AddHandler game.Window.ClientSizeChanged, AddressOf Window_ClientSizeChanged
            AddHandler game.Exiting, AddressOf Game_Exiting
            AddHandler _browser.CoreWebView2InitializationCompleted, AddressOf Browser_Initialized
            AddHandler _browser.NavigationCompleted, AddressOf Browser_NavigationCompleted
            Dim initializationTask As Task = _browser.EnsureCoreWebView2Async()
        End Sub

        Public Shared Function Attach(game As Game, Optional contentRoot As String = Nothing) As Html5EngineHost
            Return New Html5EngineHost(game, ResolveContentRoot(contentRoot))
        End Function

        Public Function ExecuteScriptAsync(script As String) As Task(Of String)
            If Not IsReady OrElse _browser.CoreWebView2 Is Nothing Then
                Throw New InvalidOperationException("The HTML5 page is not ready.")
            End If
            Return _browser.CoreWebView2.ExecuteScriptAsync(script)
        End Function

        Public Sub ResizeToWindow()
            If _disposed Then Return
            Dim bounds = _game.Window.ClientBounds
            SetWindowPos(_container.Handle, IntPtr.Zero, 0, 0, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), SWP_NOACTIVATE Or SWP_NOZORDER)
        End Sub

        Private Sub Browser_Initialized(sender As Object, e As CoreWebView2InitializationCompletedEventArgs)
            If Not e.IsSuccess Then
                RaiseEvent NavigationFailed(If(e.InitializationException?.Message, "WebView2 initialization failed."))
                Return
            End If

            Dim settings = _browser.CoreWebView2.Settings
            settings.AreDefaultContextMenusEnabled = False
            settings.AreDevToolsEnabled = Debugger.IsAttached
            settings.IsStatusBarEnabled = False
            settings.IsZoomControlEnabled = False
            _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHost, _contentRoot, CoreWebView2HostResourceAccessKind.DenyCors)
            _browser.Source = New Uri($"https://{VirtualHost}/index.html")
        End Sub

        Private Sub Browser_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
            If e.IsSuccess Then
                _IsReady = True
                RaiseEvent PageReady(Me, EventArgs.Empty)
            Else
                RaiseEvent NavigationFailed($"WebView2 navigation failed: {e.WebErrorStatus}")
            End If
        End Sub

        Private Sub Window_ClientSizeChanged(sender As Object, e As EventArgs)
            ResizeToWindow()
        End Sub

        Private Sub Game_Exiting(sender As Object, e As EventArgs)
            Dispose()
        End Sub

        Private Shared Function ResolveContentRoot(contentRoot As String) As String
            If Not String.IsNullOrWhiteSpace(contentRoot) Then Return contentRoot
            Dim candidates = {
                Path.Combine(AppContext.BaseDirectory, "Html5"),
                Path.Combine(AppContext.BaseDirectory, "Project Z HTML5")
            }
            For Each candidate In candidates
                If File.Exists(Path.Combine(candidate, "index.html")) Then Return candidate
            Next
            Throw New DirectoryNotFoundException("Pass the Project Z HTML5 directory to Html5EngineHost.Attach, or deploy it as an Html5 output folder.")
        End Function

        Private Shared Function ResolveNativeWindow(game As Game) As IntPtr
            ' MonoGame Native exposes an SDL handle through GameWindow.Handle,
            ' not necessarily the Win32 HWND required by child controls.
            Dim currentProcess As System.Diagnostics.Process = System.Diagnostics.Process.GetCurrentProcess()
            currentProcess.Refresh()
            If currentProcess.MainWindowHandle <> IntPtr.Zero AndAlso IsWindow(currentProcess.MainWindowHandle) Then Return currentProcess.MainWindowHandle
            If game.Window.Handle <> IntPtr.Zero AndAlso IsWindow(game.Window.Handle) Then Return game.Window.Handle
            Return IntPtr.Zero
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _game.Window.ClientSizeChanged, AddressOf Window_ClientSizeChanged
            RemoveHandler _game.Exiting, AddressOf Game_Exiting
            RemoveHandler _browser.CoreWebView2InitializationCompleted, AddressOf Browser_Initialized
            RemoveHandler _browser.NavigationCompleted, AddressOf Browser_NavigationCompleted
            _browser.Dispose()
            _container.Dispose()
        End Sub

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function SetWindowPos(window As IntPtr, insertAfter As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, flags As UInteger) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function IsWindow(window As IntPtr) As Boolean
        End Function

        Private NotInheritable Class NativeChildHost
            Inherits Forms.UserControl

            Private Const WS_CHILD As Integer = &H40000000
            Private Const WS_CLIPCHILDREN As Integer = &H2000000
            Private Const WS_VISIBLE As Integer = &H10000000
            Private ReadOnly _parentWindow As IntPtr

            Public Sub New(parentWindow As IntPtr)
                _parentWindow = parentWindow
                SetStyle(Forms.ControlStyles.AllPaintingInWmPaint Or Forms.ControlStyles.Opaque, True)
            End Sub

            Protected Overrides ReadOnly Property CreateParams As Forms.CreateParams
                Get
                    Dim parameters = MyBase.CreateParams
                    parameters.Parent = _parentWindow
                    parameters.Style = parameters.Style Or WS_CHILD Or WS_VISIBLE Or WS_CLIPCHILDREN
                    Return parameters
                End Get
            End Property
        End Class
    End Class
End Namespace
