Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.ShaderEditor
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.Designer
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition
Imports Forms = System.Windows.Forms

Namespace Windows.ShaderEditing
    Friend NotInheritable Class ShaderEditorScene
        Inherits Scene

#If PROJECTZ_DIRECTX12 Then
        Private Const Backend As ShaderBackend = ShaderBackend.DirectX12
#Else
        Private Const Backend As ShaderBackend = ShaderBackend.DirectX11
#End If
        Private Const GraphFilter As String = "Project-Z shader graph (*.pzshader)|*.pzshader"
        Private ReadOnly root As CanvasPanel
        Private ReadOnly markup As SceneXamlParser
        Private ReadOnly workspace As CanvasPanel
        Private ReadOnly simple As SimpleShaderView
        Private ReadOnly advanced As AdvancedShaderView
        Private ReadOnly image As ImageElement
        Private ReadOnly prompt As CanvasPanel
        Private ReadOnly owner As Forms.IWin32Window
        Private ReadOnly publish As Action(Of Byte(), String)
        Private ReadOnly playback As New Stopwatch()
        Private ReadOnly history As New List(Of String)
        Private ReadOnly staging As String = CompilerStagingDirectory()
        Private ReadOnly compiler As String = Path.Combine(AppContext.BaseDirectory, "ProjectZ.ShaderCompiler")
        Private document As ShaderDocument
        Private documentPath As String
        Private saved As String
        Private historyIndex As Integer
        Private diagnostics As String = ""
        Private generatedSource As String = ""
        Private viewingSource As Boolean
        Private pendingAction As Action
        Private compile As Task(Of ShaderCompileResult)
        Private cancellation As CancellationTokenSource
        Private compiledPath As String
        Private compiledGraph As String
        Private pendingRun As Long
        Private nextPreview As Long
        Private theme As New WindowAppearanceSettings

        Private Shared Function CompilerStagingDirectory() As String
            Dim configured = Environment.GetEnvironmentVariable("PROJECTZ_SHADER_EDITOR_STAGING")
            If Not String.IsNullOrWhiteSpace(configured) Then Return Path.GetFullPath(configured)
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectZ", "ShaderEditor", "Compiler")
        End Function

        Friend Sub New(manager As SceneManager, owner As Forms.IWin32Window, path As String, graphJson As String,
                       publish As Action(Of Byte(), String), minimize As Action, maximize As Action, close As Action)
            MyBase.New(manager)
            Me.owner = owner
            Me.publish = publish
            isCursorVisible = False
            EnableDesignTimeControls = False
            Dim parser As SceneXamlParser = Nothing
            root = DirectCast(ShaderEditorChrome.LoadView(Me, "ShaderEditorWindow.xaml", parser), CanvasPanel)
            markup = parser
            workspace = Find(Of CanvasPanel)("Workspace")
            prompt = Find(Of CanvasPanel)("SavePrompt")
            image = Find(Of ImageElement)("PreviewImage")
            simple = New SimpleShaderView(Me)
            advanced = New AdvancedShaderView(Me)
            workspace.Children.Add(simple.Root)
            workspace.Children.Add(advanced.Root)
            AddElement(root)
            AddHandler simple.GraphChanged, AddressOf GraphChanged
            AddHandler advanced.GraphChanged, AddressOf GraphChanged
            Bind("New", Sub() Confirm(Sub() SetDocument(ShaderDocument.Create(), Nothing)))
            Bind("Open", Sub() Confirm(AddressOf OpenDocument))
            Bind("Save", Sub() SaveDocument())
            Bind("ExportSource", AddressOf ExportSource)
            Bind("ExportEffect", AddressOf ExportEffect)
            Bind("Undo", Sub() HistoryStep(-1))
            Bind("Redo", Sub() HistoryStep(1))
            Bind("Simple", Sub() ChangeMode(ShaderMode.Simple))
            Bind("Advanced", Sub() ChangeMode(ShaderMode.Advanced))
            Bind("Run", AddressOf RunShader)
            Bind("Play", AddressOf TogglePlayback)
            Bind("LoadImage", AddressOf LoadImage)
            Bind("ResetImage", AddressOf ResetImage)
            Bind("DiagnosticsTab", Sub() ShowCompilerTab(False))
            Bind("SourceTab", Sub() ShowCompilerTab(True))
            Bind("Minimize", minimize)
            Bind("Maximize", maximize)
            Bind("Close", Sub() Confirm(close))
            Bind("PromptSave", Sub()
                                   If SaveDocument() Then CompletePrompt(True)
                               End Sub)
            Bind("PromptDiscard", Sub() CompletePrompt(True))
            Bind("PromptCancel", Sub() CompletePrompt(False))
            AddHandler Find(Of CheckBox)("AutoRun").CheckedChanged, Sub(oldValue, newValue) QueueAutoRun()
            AddHandler OnKeyDown, AddressOf HandleShortcut
            Find(Of TextElement)("Backend").Text = If(Backend = ShaderBackend.DirectX12, "DirectX 12", "DirectX 11") & " · XAML pixel effect"
            MakeCheckerboard()
            ResetImage()
            SetDocument(If(graphJson Is Nothing, ShaderDocument.Create(), ShaderDocument.Parse(graphJson)), Nothing)
            If path Is Nothing OrElse TryOpen(path) Then StartCompile(False)
        End Sub

        Private Function Find(Of T As SceneElement)(name As String) As T
            Return markup.FindName(Of T)(name)
        End Function

        Private Sub Bind(name As String, action As Action)
            AddHandler Find(Of Button)(name).MouseLeftClick, Sub(point) action()
        End Sub

        Friend Sub Arrange(width As Integer, height As Integer)
            Dim workWidth = width - 310.0F
            Dim workHeight = height - 114.0F
            root.Size = New Vector2(width, height)
            Dim header = Find(Of CanvasPanel)("Header"), toolbar = Find(Of CanvasPanel)("Toolbar")
            Dim rail = Find(Of CanvasPanel)("PreviewRail"), footer = Find(Of CanvasPanel)("Footer")
            header.Size = New Vector2(width, header.Size.Y)
            toolbar.Size = New Vector2(width, toolbar.Size.Y)
            root.SetChildPosition(rail, New Vector2(workWidth + 2, 88))
            rail.Size = New Vector2(308, workHeight)
            workspace.Size = New Vector2(workWidth, workHeight)
            simple.Root.Size = workspace.Size
            advanced.Root.Size = workspace.Size
            root.SetChildPosition(footer, New Vector2(0, height - 26))
            footer.Size = New Vector2(width, 26)
            root.SetChildPosition(prompt, New Vector2((width - prompt.Size.X) / 2, (height - prompt.Size.Y) / 2))
            header.SetChildPosition(Find(Of Button)("Minimize"), New Vector2(width - 126, 6))
            header.SetChildPosition(Find(Of Button)("Maximize"), New Vector2(width - 86, 6))
            header.SetChildPosition(Find(Of Button)("Close"), New Vector2(width - 46, 6))
            toolbar.SetChildPosition(Find(Of CheckBox)("AutoRun"), New Vector2(width - 204, 9))
            toolbar.SetChildPosition(Find(Of Button)("Run"), New Vector2(width - 100, 6))
            footer.SetChildPosition(Find(Of TextElement)("Backend"), New Vector2(width - 224, 5))
            Find(Of Textbox)("CompilerPanel").Size = New Vector2(284, Math.Max(42, workHeight - 334))
            RequestRender()
        End Sub

        Friend Sub ApplyAppearance(settings As WindowAppearanceSettings)
            theme = settings
            BackgroundColor = settings.Background(settings.BackgroundColor)
            ShaderEditorChrome.ApplyTheme(root, settings)
            simple.ApplyAppearance(settings)
            advanced.ApplyAppearance(settings)
            Find(Of TextElement)("Brand").ForegroundColor = settings.AccentColor
            StyleSelection()
            RequestRender()
        End Sub

        Private Sub StyleSelection()
            Find(Of Button)("Simple").BackgroundColor = If(document.Mode = ShaderMode.Simple, Color.Lerp(theme.SurfaceColor, theme.AccentColor, 0.26F), theme.SurfaceColor)
            Find(Of Button)("Advanced").BackgroundColor = If(document.Mode = ShaderMode.Advanced, Color.Lerp(theme.SurfaceColor, theme.AccentColor, 0.26F), theme.SurfaceColor)
            Find(Of Button)("DiagnosticsTab").BackgroundColor = If(Not viewingSource, Color.Lerp(theme.SurfaceColor, theme.AccentColor, 0.22F), theme.SurfaceColor)
            Find(Of Button)("SourceTab").BackgroundColor = If(viewingSource, Color.Lerp(theme.SurfaceColor, theme.AccentColor, 0.22F), theme.SurfaceColor)
        End Sub

        Private Sub SetDocument(nextDocument As ShaderDocument, path As String)
            cancellation?.Cancel()
            document = nextDocument : documentPath = path
            saved = Snapshot()
            history.Clear() : history.Add(saved) : historyIndex = 0
            image.VisualEffect?.Dispose() : image.VisualEffect = Nothing
            BindDocument()
            UpdateDocument()
            SetStatus("Run to compile and preview the shader.")
            QueueAutoRun()
        End Sub

        Private Sub BindDocument()
            simple.SetDocument(document)
            advanced.SetDocument(document)
            ChangeMode(document.Mode)
        End Sub

        Private Sub ChangeMode(mode As ShaderMode)
            document.Mode = mode
            simple.Root.isVisible = mode = ShaderMode.Simple
            advanced.Root.isVisible = mode = ShaderMode.Advanced
            If mode = ShaderMode.Simple Then simple.Refresh() Else advanced.Refresh()
            StyleSelection()
            RequestRender()
        End Sub

        Private Function Snapshot() As String
            Dim mode = document.Mode
            Try
                document.Mode = ShaderMode.Simple
                Return JsonSerializer.Serialize(document)
            Finally
                document.Mode = mode
            End Try
        End Function

        Private Sub GraphChanged(sender As Object, args As EventArgs)
            Dim snapshotValue = Snapshot()
            If snapshotValue = history(historyIndex) Then Return
            history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1)
            history.Add(snapshotValue)
            If history.Count > 64 Then history.RemoveAt(0)
            historyIndex = history.Count - 1
            cancellation?.Cancel()
            UpdateDocument()
            SetStatus("Graph changed · Run to update the preview")
            QueueAutoRun()
        End Sub

        Private Sub HistoryStep(direction As Integer)
            Dim nextIndex = historyIndex + direction
            If nextIndex < 0 OrElse nextIndex >= history.Count Then Return
            cancellation?.Cancel()
            historyIndex = nextIndex
            Dim mode = document.Mode, name = document.Name
            document = JsonSerializer.Deserialize(Of ShaderDocument)(history(historyIndex))
            document.Mode = mode : document.Name = name
            BindDocument()
            UpdateDocument()
            QueueAutoRun()
        End Sub

        Private Sub UpdateDocument()
            Find(Of TextElement)("DocumentTitle").Text = document.Name & If(history(historyIndex) <> saved, " •", "")
            Find(Of Button)("Undo").isEnabled = historyIndex > 0
            Find(Of Button)("Redo").isEnabled = historyIndex < history.Count - 1
            Try
                generatedSource = ShaderGenerator.Generate(document)
            Catch ex As InvalidDataException
                generatedSource = ex.Message
            End Try
            If viewingSource Then Find(Of Textbox)("CompilerPanel").Text = generatedSource
            RequestRender()
        End Sub

        Private Sub QueueAutoRun()
            pendingRun = If(Find(Of CheckBox)("AutoRun").IsChecked.GetValueOrDefault(), Stopwatch.GetTimestamp() + Stopwatch.Frequency * 2 \ 5, 0L)
        End Sub

        Private Sub StartCompile(shouldPublish As Boolean, Optional export As String = Nothing)
            If compile IsNot Nothing Then Return
            pendingRun = 0
            Try
                generatedSource = ShaderGenerator.Generate(document)
                compiledPath = export
                compiledGraph = If(shouldPublish, document.ToJson(), Nothing)
                cancellation = New CancellationTokenSource()
                compile = ShaderCompiler.CompileAsync(generatedSource, Backend, compiler, staging, cancellation.Token)
                SetStatus("Compiling shader…")
                Find(Of Button)("Run").isEnabled = False
            Catch ex As Exception When TypeOf ex Is InvalidDataException OrElse TypeOf ex Is IOException
                SetError("Shader compilation failed", ex.Message)
            End Try
        End Sub

        Private Sub RunShader()
            ClearEditorFocus()
            StartCompile(True)
        End Sub

        Friend Sub UpdateEditor()
            If compile IsNot Nothing AndAlso compile.IsCompleted Then FinishCompile()
            Dim now = Stopwatch.GetTimestamp()
            If compile Is Nothing AndAlso pendingRun > 0 AndAlso now >= pendingRun Then StartCompile(True)
            If playback.IsRunning AndAlso now >= nextPreview Then
                nextPreview = now + Stopwatch.Frequency \ 24
                Find(Of TextElement)("PreviewTime").Text = playback.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) & " s"
                If image.VisualEffect IsNot Nothing Then image.VisualEffect.TimeOverride = CSng(playback.Elapsed.TotalSeconds)
                RequestRender()
            End If
        End Sub

        Private Sub FinishCompile()
            Try
                If cancellation.IsCancellationRequested Then Return
                Dim result = compile.GetAwaiter().GetResult()
                If Not result.Success Then
                    SetError("Shader compilation failed", result.Diagnostics)
                    Return
                End If
                Dim messages = result.Diagnostics.Split({ControlChars.CrLf, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries).
                    Where(Function(line) Not line.StartsWith("Compiled '", StringComparison.Ordinal)).ToArray()
                diagnostics = If(messages.Length = 0, "Compiled successfully.", String.Join(Environment.NewLine, messages))
                If compiledPath IsNot Nothing Then
                    File.WriteAllBytes(compiledPath, result.Bytecode)
                    SetStatus("Exported " & Path.GetFileName(compiledPath))
                Else
                    Dim replacement = XamlNativeEffect.FromCompiledShader(result.Bytecode)
                    replacement.TimeOverride = CSng(playback.Elapsed.TotalSeconds)
                    image.VisualEffect?.Dispose()
                    image.VisualEffect = replacement
                    SetStatus("Shader running")
                    If compiledGraph IsNot Nothing Then
                        Dim graphFits = Encoding.UTF8.GetByteCount(compiledGraph) <= ShaderDocument.MaximumGraphBytes
                        If Not graphFits Then SetStatus("Shader running · graph exceeds the 256 KiB transfer limit")
                        publish(result.Bytecode, If(graphFits, compiledGraph, Nothing))
                    End If
                End If
                ShowCompilerTab(viewingSource)
            Catch ex As Exception
                SetError("Shader compilation failed", ex.Message)
            Finally
                cancellation.Dispose() : cancellation = Nothing : compile = Nothing
                compiledGraph = Nothing
                Find(Of Button)("Run").isEnabled = True
            End Try
        End Sub

        Private Sub ShowCompilerTab(showSource As Boolean)
            viewingSource = showSource
            Find(Of Textbox)("CompilerPanel").Text = If(viewingSource, generatedSource, diagnostics)
            StyleSelection()
            RequestRender()
        End Sub

        Private Sub SetStatus(message As String)
            Find(Of TextElement)("Status").Text = message
            RequestRender()
        End Sub

        Private Sub SetError(message As String, detail As String)
            diagnostics = detail
            ShowCompilerTab(False)
            SetStatus(message)
        End Sub

        Friend Sub Confirm(action As Action)
            ClearEditorFocus()
            If history(historyIndex) = saved Then
                action()
                Return
            End If
            pendingAction = action
            prompt.isVisible = True
            workspace.isEnabled = False
            Find(Of CanvasPanel)("Toolbar").isEnabled = False
            RequestRender()
        End Sub

        Private Sub CompletePrompt(continueAction As Boolean)
            Dim action = pendingAction
            pendingAction = Nothing
            prompt.isVisible = False
            workspace.isEnabled = True
            Find(Of CanvasPanel)("Toolbar").isEnabled = True
            If continueAction Then action?.Invoke()
            RequestRender()
        End Sub

        Private Sub OpenDocument()
            Using dialog As New Forms.OpenFileDialog With {.Filter = GraphFilter, .CheckFileExists = True}
                If dialog.ShowDialog(owner) = Forms.DialogResult.OK Then TryOpen(dialog.FileName)
            End Using
        End Sub

        Private Function TryOpen(path As String) As Boolean
            Try
                SetDocument(ShaderDocument.Load(path), path)
                Return True
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                SetError("Could not open shader graph", ex.Message)
                Return False
            End Try
        End Function

        Private Function SaveDocument() As Boolean
            ClearEditorFocus()
            Dim path = If(documentPath, "")
            If path.Length = 0 Then path = ChooseSave(GraphFilter, "pzshader")
            If path Is Nothing Then Return False
            Dim previous = document.Name
            Try
                document.Name = IO.Path.GetFileNameWithoutExtension(path)
                document.Save(path)
                documentPath = path
                saved = Snapshot() : history(historyIndex) = saved
                UpdateDocument()
                SetStatus("Saved " & IO.Path.GetFileName(path))
                Return True
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is InvalidDataException
                document.Name = previous
                SetError("Could not save shader graph", ex.Message)
                Return False
            End Try
        End Function

        Private Function ChooseSave(filter As String, extension As String) As String
            Using dialog As New Forms.SaveFileDialog With {.Filter = filter, .DefaultExt = extension, .FileName = document.Name}
                Return If(dialog.ShowDialog(owner) = Forms.DialogResult.OK, dialog.FileName, Nothing)
            End Using
        End Function

        Private Sub ExportSource()
            ClearEditorFocus()
            Dim path = ChooseSave("Effect source (*.fx)|*.fx", "fx")
            If path Is Nothing Then Return
            Try
                File.WriteAllText(path, ShaderGenerator.Generate(document))
                SetStatus("Exported " & IO.Path.GetFileName(path))
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is InvalidDataException
                SetError("Could not export FX source", ex.Message)
            End Try
        End Sub

        Private Sub ExportEffect()
            If compile IsNot Nothing Then Return
            ClearEditorFocus()
            Dim path = ChooseSave("Compiled XAML effect (*.mgfx)|*.mgfx", "mgfx")
            If path IsNot Nothing Then StartCompile(False, path)
        End Sub

        Private Sub LoadImage()
            Using dialog As New Forms.OpenFileDialog With {.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff", .CheckFileExists = True}
                If dialog.ShowDialog(owner) <> Forms.DialogResult.OK Then Return
                Try
                    image.Load(dialog.FileName)
                    RequestRender()
                Catch ex As Exception
                    SetError("Could not load preview image", ex.Message)
                End Try
            End Using
        End Sub

        Private Sub ResetImage()
            Const width = 320, height = 216
            Dim pixels(width * height - 1) As Color
            For y = 0 To height - 1
                For x = 0 To width - 1
                    If x < 10 OrElse y < 10 OrElse x >= width - 10 OrElse y >= height - 10 Then Continue For
                    Dim pixel = Color.Lerp(New Color(34, 77, 144), New Color(116, 58, 147), x / CSng(width))
                    If Vector2.DistanceSquared(New Vector2(x, y), New Vector2(184, 88)) < 46 * 46 Then pixel = Color.Lerp(New Color(255, 185, 103), New Color(248, 93, 107), y / CSng(height))
                    If y > 172 - 63 * MathF.Sin(x / 41.0F) Then pixel = New Color(38, 167, 169)
                    If y > 185 - 27 * MathF.Sin(x / 21.0F) Then pixel = New Color(16, 65, 84)
                    If Vector2.DistanceSquared(New Vector2(x, y), New Vector2(266, 173)) < 21 * 21 Then pixel = Color.FromNonPremultiplied(237, 249, 255, 158)
                    pixels(y * width + x) = pixel
                Next
            Next
            image.LoadPixels(width, height, pixels)
            RequestRender()
        End Sub

        Private Sub MakeCheckerboard()
            Const width = 284, height = 195
            Dim pixels(width * height - 1) As Color
            For y = 0 To height - 1
                For x = 0 To width - 1
                    pixels(y * width + x) = If((x \ 12 + y \ 12) Mod 2 = 0, New Color(32, 39, 43), New Color(46, 55, 58))
                Next
            Next
            Find(Of ImageElement)("Checkerboard").LoadPixels(width, height, pixels)
        End Sub

        Private Sub TogglePlayback()
            If playback.IsRunning Then playback.Stop() Else playback.Start()
            Find(Of Button)("Play").Text = If(playback.IsRunning, "Pause time", "Play time")
        End Sub

        Private Sub HandleShortcut(key As Keys, state As KeyboardState)
            If prompt.isVisible Then
                If key = Keys.Escape Then CompletePrompt(False)
                Return
            End If
            If Not (state.IsKeyDown(Keys.LeftControl) OrElse state.IsKeyDown(Keys.RightControl)) Then Return
            Select Case key
                Case Keys.N : Confirm(Sub() SetDocument(ShaderDocument.Create(), Nothing))
                Case Keys.O : Confirm(AddressOf OpenDocument)
                Case Keys.S : SaveDocument()
                Case Keys.Enter : RunShader()
                Case Keys.Z : HistoryStep(-1)
                Case Keys.Y : HistoryStep(1)
            End Select
        End Sub

        Friend Sub CancelCompilation()
            cancellation?.Cancel()
        End Sub

        Private Sub ClearEditorFocus()
            If document.Mode = ShaderMode.Advanced Then advanced.CommitEdits()
            FocusElement(Nothing)
        End Sub
    End Class
End Namespace
