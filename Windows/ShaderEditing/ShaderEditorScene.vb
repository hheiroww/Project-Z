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
    Partial Friend NotInheritable Class ShaderEditorScene
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
        Private ReadOnly material As SimpleMaterialView
        Private ReadOnly advanced As AdvancedShaderView
        Private ReadOnly image As ImageElement
        Private ReadOnly prompt As CanvasPanel
        Private ReadOnly owner As Forms.IWin32Window
        Private ReadOnly publish As Action(Of Byte(), String)
        Private ReadOnly takeReceipt As Func(Of ShaderGraphReceipt)
        Private ReadOnly closeAction As Action
        Private ReadOnly hosted, surface As Boolean
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
        Private applyingGraph, applyingSnapshot As String
        Private applyingDeadline As Long
        Private applyContinuesPrompt, closeAfterSave As Boolean
        Private saveTask As Task(Of (Name As String, Snapshot As String))
        Private saveDestination As String
        Private saveContinuesPrompt, exportGraphOnly As Boolean
        Private compile As Task(Of ShaderCompileResult)
        Private cancellation As CancellationTokenSource
        Private compiledPath As String
        Private compiledGraph As String
        Private pendingRun As Long
        Private pendingRunPublishes As Boolean
        Private nextPreview As Long
        Private theme As New WindowAppearanceSettings

        Private Shared Function CompilerStagingDirectory() As String
            Dim configured = Environment.GetEnvironmentVariable("PROJECTZ_SHADER_EDITOR_STAGING")
            If Not String.IsNullOrWhiteSpace(configured) Then Return Path.GetFullPath(configured)
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectZ", "ShaderEditor", "Compiler")
        End Function

        Friend Sub New(manager As SceneManager, owner As Forms.IWin32Window, path As String, graphJson As String,
                       publish As Action(Of Byte(), String), minimize As Action, maximize As Action, close As Action,
                       takeReceipt As Func(Of ShaderGraphReceipt), hosted As Boolean, surface As Boolean)
            MyBase.New(manager)
            Me.owner = owner
            Me.publish = publish
            Me.takeReceipt = takeReceipt
            closeAction = close
            Me.hosted = hosted
            Me.surface = surface
            isCursorVisible = False
            EnableDesignTimeControls = False
            Dim parser As SceneXamlParser = Nothing
            root = DirectCast(ShaderEditorChrome.LoadView(Me, "ShaderEditorWindow.xaml", parser), CanvasPanel)
            markup = parser
            workspace = Find(Of CanvasPanel)("Workspace")
            prompt = Find(Of CanvasPanel)("SavePrompt")
            image = Find(Of ImageElement)("PreviewImage")
            simple = New SimpleShaderView(Me)
            material = New SimpleMaterialView(Me)
            advanced = New AdvancedShaderView(Me)
            workspace.Children.Add(simple.Root)
            workspace.Children.Add(material.Root)
            workspace.Children.Add(advanced.Root)
            AddElement(root)
            AddHandler simple.GraphChanged, AddressOf GraphChanged
            AddHandler material.GraphChanged, AddressOf GraphChanged
            AddHandler advanced.GraphChanged, AddressOf GraphChanged
            Bind("New", Sub() Confirm(Sub() SetDocument(CreateNewDocument(), Nothing, keepSaved:=hosted)))
            Bind("Open", Sub() Confirm(AddressOf OpenDocument))
            Bind("Save", Sub() StartSave(exportOnly:=hosted))
            Bind("ExportSource", AddressOf ExportSource)
            Bind("ExportEffect", AddressOf ExportEffect)
            Bind("Undo", Sub() HistoryStep(-1))
            Bind("Redo", Sub() HistoryStep(1))
            Bind("Simple", Sub() SwitchMode(ShaderMode.Simple))
            Bind("Advanced", Sub() SwitchMode(ShaderMode.Advanced))
            SetUpPresetBrowser()
            Bind("Run", AddressOf RunShader)
            Bind("Play", AddressOf TogglePlayback)
            Bind("LoadImage", AddressOf LoadImage)
            Bind("ResetImage", AddressOf ResetImage)
            Bind("DiagnosticsTab", Sub() ShowCompilerTab(False))
            Bind("SourceTab", Sub() ShowCompilerTab(True))
            Bind("Minimize", minimize)
            Bind("Maximize", maximize)
            Bind("Close", AddressOf RequestClose)
            Bind("PromptSave", Sub()
                                   If hosted Then
                                       ApplyGraph(continuePrompt:=True)
                                   Else
                                       StartSave(continuePrompt:=True)
                                   End If
                               End Sub)
            Bind("PromptDiscard", Sub() CompletePrompt(True))
            Bind("PromptCancel", Sub() CompletePrompt(False))
            AddHandler Find(Of CheckBox)("AutoRun").CheckedChanged, Sub(oldValue, newValue) QueueAutoRun()
            AddHandler OnKeyDown, AddressOf HandleShortcut
            If hosted Then
                Find(Of Button)("Save").Text = "Export graph"
                Find(Of Button)("Save").Size = New Vector2(140, 30)
                Find(Of Button)("Run").Text = "Apply"
                Find(Of Button)("PromptSave").Text = "Apply"
                Find(Of TextElement)("PromptMessage").Text = "Apply changes to the clip?"
                Find(Of CanvasPanel)("Toolbar").SetChildPosition(Find(Of Button)("ExportSource"), New Vector2(284, 6))
                Find(Of Button)("ExportEffect").isVisible = False
            End If
            MakeCheckerboard()
            ResetImage()
            SetDocument(If(graphJson Is Nothing, CreateNewDocument(), ShaderDocument.Parse(graphJson)), Nothing)
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
            material.Root.Size = workspace.Size
            advanced.Root.Size = workspace.Size
            root.SetChildPosition(footer, New Vector2(0, height - 26))
            footer.Size = New Vector2(width, 26)
            root.SetChildPosition(prompt, New Vector2((width - prompt.Size.X) / 2, (height - prompt.Size.Y) / 2))
            ArrangePresetBrowser(width, height)
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
            material.ApplyAppearance(settings)
            advanced.ApplyAppearance(settings)
            StylePresetBrowser(settings)
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

        Private Function CreateNewDocument() As ShaderDocument
            Return If(surface, SurfaceMaterials.CreatePreset("clear-water"), ShaderDocument.Create())
        End Function

        Private Sub SetDocument(nextDocument As ShaderDocument, path As String, Optional keepSaved As Boolean = False)
            cancellation?.Cancel()
            document = nextDocument : documentPath = path
            Dim initial = Snapshot()
            If Not keepSaved Then saved = initial
            history.Clear() : history.Add(initial) : historyIndex = 0
            image.VisualEffect?.Dispose() : image.VisualEffect = Nothing
            BindDocument()
            UpdateDocument()
            SetStatus(If(hosted, "Apply to save the graph to the clip.", "Run to compile and preview the graph."))
            QueueAutoRun()
        End Sub

        Private Sub BindDocument()
            simple.SetDocument(document)
            material.SetDocument(document)
            advanced.SetDocument(document)
            Dim showMediaControls = Not document.IsSurface
            Find(Of Button)("LoadImage").isVisible = showMediaControls
            Find(Of Button)("ResetImage").isVisible = showMediaControls
            Find(Of Button)("ExportEffect").isVisible = Not hosted AndAlso showMediaControls
            Find(Of TextElement)("Backend").Text = If(Backend = ShaderBackend.DirectX12, "DirectX 12", "DirectX 11") &
                If(document.IsSurface, " · Material preview", " · XAML pixel effect")
            ChangeMode(document.Mode)
        End Sub

        Private Sub ChangeMode(mode As ShaderMode)
            document.Mode = mode
            simple.Root.isVisible = mode = ShaderMode.Simple AndAlso Not document.IsSurface
            material.Root.isVisible = mode = ShaderMode.Simple AndAlso document.IsSurface
            advanced.Root.isVisible = mode = ShaderMode.Advanced
            If material.Root.isVisible Then
                material.Refresh()
            ElseIf simple.Root.isVisible Then
                simple.Refresh()
            Else
                advanced.Refresh()
            End If
            StyleSelection()
            RequestRender()
        End Sub

        Private Sub SwitchMode(mode As ShaderMode)
            ClearEditorFocus()
            ChangeMode(mode)
        End Sub

        Private Function Snapshot(Optional value As ShaderDocument = Nothing) As String
            value = If(value, document)
            Dim mode = value.Mode
            Try
                value.Mode = ShaderMode.Simple
                Return JsonSerializer.Serialize(value)
            Finally
                value.Mode = mode
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
            SetStatus(If(hosted, "Graph changed · Apply to update the clip", "Graph changed · Run to update the preview"))
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
            If viewingSource Then ShowCompilerTab(True)
            RequestRender()
        End Sub

        Private Sub QueueAutoRun()
            pendingRunPublishes = Not hosted
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
                If applyingGraph Is Nothing Then SetStatus("Compiling preview…")
                RefreshModalState()
            Catch ex As Exception When TypeOf ex Is InvalidDataException OrElse TypeOf ex Is IOException
                SetError("Shader compilation failed", ex.Message)
            End Try
        End Sub

        Private Sub RunShader()
            ClearEditorFocus()
            If hosted Then
                ApplyGraph()
            Else
                StartCompile(True)
            End If
        End Sub

        Friend Sub UpdateEditor()
            If saveTask IsNot Nothing AndAlso saveTask.IsCompleted Then FinishSave()
            Dim receipt = takeReceipt()
            While receipt IsNot Nothing
                FinishApply(receipt)
                receipt = takeReceipt()
            End While
            If compile IsNot Nothing AndAlso compile.IsCompleted Then FinishCompile()
            Dim now = Stopwatch.GetTimestamp()
            If applyingGraph IsNot Nothing AndAlso now >= applyingDeadline Then
                FinishApply(New ShaderGraphReceipt(applyingGraph, "The host did not finish applying the shader. Try applying it again."))
            End If
            If compile Is Nothing AndAlso pendingRun > 0 AndAlso now >= pendingRun Then StartCompile(pendingRunPublishes)
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
                    If Not hosted Then SetStatus(If(compiledGraph Is Nothing, "Preview ready · Run to apply", "Shader running"))
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
                RefreshModalState()
            End Try
        End Sub

        Private Sub ShowCompilerTab(showSource As Boolean)
            viewingSource = showSource
            If showSource Then
                Try
                    generatedSource = If(document.IsSurface, SurfaceMaterials.GenerateHlsl(document), ShaderGenerator.Generate(document))
                Catch ex As InvalidDataException
                    generatedSource = ex.Message
                End Try
            End If
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
            If pendingAction IsNot Nothing OrElse saveTask IsNot Nothing Then Return
            ClearEditorFocus()
            If history(historyIndex) = saved Then
                action()
                Return
            End If
            pendingAction = action
            prompt.isVisible = True
            If applyingGraph IsNot Nothing AndAlso applyingSnapshot = history(historyIndex) Then applyContinuesPrompt = True
            RefreshModalState()
            RequestRender()
        End Sub

        Friend Sub RequestClose()
            If saveTask IsNot Nothing Then
                closeAfterSave = True
                Return
            End If
            If presetBrowser.isVisible Then ShowPresetBrowser(False)
            Confirm(closeAction)
        End Sub

        Private Sub RefreshModalState()
            Dim writable = saveTask Is Nothing
            Dim editing = writable AndAlso Not prompt.isVisible AndAlso Not presetBrowser.isVisible
            workspace.isEnabled = editing
            Find(Of CanvasPanel)("Toolbar").isEnabled = editing
            Find(Of CanvasPanel)("PreviewRail").isEnabled = editing
            Find(Of Button)("PromptSave").isEnabled = writable AndAlso applyingGraph Is Nothing
            Find(Of Button)("PromptDiscard").isEnabled = writable
            Find(Of Button)("PromptCancel").isEnabled = writable
            Find(Of Button)("Run").isEnabled = If(hosted, applyingGraph Is Nothing, compile Is Nothing)
        End Sub

        Private Sub CompletePrompt(continueAction As Boolean)
            If saveTask IsNot Nothing Then Return
            Dim action = pendingAction
            pendingAction = Nothing
            applyContinuesPrompt = False
            prompt.isVisible = False
            RefreshModalState()
            If continueAction Then action?.Invoke()
            RequestRender()
        End Sub

        Private Sub ApplyGraph(Optional continuePrompt As Boolean = False)
            If applyingGraph IsNot Nothing Then Return
            Try
                Dim graph = document.ToJson()
                If Encoding.UTF8.GetByteCount(graph) > ShaderDocument.MaximumGraphBytes Then
                    Throw New InvalidDataException("The graph exceeds the 256 KiB transfer limit.")
                End If
                applyingGraph = graph
                applyingSnapshot = Snapshot()
                applyingDeadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 15
                applyContinuesPrompt = continuePrompt
                publish(Array.Empty(Of Byte)(), graph)
                SetStatus("Applying to the clip…")
                RefreshModalState()
                pendingRunPublishes = False
                pendingRun = 1
            Catch ex As InvalidDataException
                SetError("Could not apply graph", ex.Message)
            End Try
        End Sub

        Private Sub FinishApply(receipt As ShaderGraphReceipt)
            If applyingGraph Is Nothing OrElse Not String.Equals(applyingGraph, receipt.Graph, StringComparison.Ordinal) Then Return
            Dim snapshotValue = applyingSnapshot
            Dim continuePrompt = applyContinuesPrompt
            applyingGraph = Nothing : applyingSnapshot = Nothing : applyContinuesPrompt = False : applyingDeadline = 0
            RefreshModalState()
            If receipt.Error IsNot Nothing Then
                SetError("Could not apply graph", receipt.Error)
                Return
            End If
            saved = snapshotValue
            UpdateDocument()
            SetStatus("Applied to the clip")
            If continuePrompt AndAlso history(historyIndex) = saved Then CompletePrompt(True)
        End Sub

        Private Sub OpenDocument()
            Using dialog As New Forms.OpenFileDialog With {.Filter = GraphFilter, .CheckFileExists = True}
                If dialog.ShowDialog(owner) = Forms.DialogResult.OK Then TryOpen(dialog.FileName)
            End Using
        End Sub

        Private Function TryOpen(path As String) As Boolean
            Try
                Dim opened = ShaderDocument.Load(path)
                If hosted AndAlso opened.IsSurface <> surface Then
                    Throw New InvalidDataException(If(surface, "Select a material graph.", "Select a video shader graph."))
                End If
                SetDocument(opened, If(hosted, Nothing, path), keepSaved:=hosted)
                Return True
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is InvalidDataException
                SetError("Could not open shader graph", ex.Message)
                Return False
            End Try
        End Function

        Private Sub StartSave(Optional continuePrompt As Boolean = False, Optional exportOnly As Boolean = False)
            If saveTask IsNot Nothing Then Return
            ClearEditorFocus()
            Dim path = If(exportOnly, "", If(documentPath, ""))
            If path.Length = 0 Then path = ChooseSave(GraphFilter, "pzshader")
            If path Is Nothing Then Return
            Dim snapshotValue = history(historyIndex)
            Dim mode = document.Mode
            Dim name = document.Name
            saveDestination = path
            saveContinuesPrompt = continuePrompt
            exportGraphOnly = exportOnly
            saveTask = Task.Run(Function()
                                    Dim writing = JsonSerializer.Deserialize(Of ShaderDocument)(snapshotValue)
                                    writing.Mode = mode
                                    writing.Name = If(exportOnly, name, IO.Path.GetFileNameWithoutExtension(path))
                                    writing.Save(path)
                                    Return (Name:=writing.Name, Snapshot:=Snapshot(writing))
                                End Function)
            SetStatus(If(exportOnly, "Exporting graph…", "Saving graph…"))
            RefreshModalState()
        End Sub

        Private Sub FinishSave()
            Dim finished = saveTask
            Dim path = saveDestination
            Dim continuePrompt = saveContinuesPrompt
            Dim exportOnly = exportGraphOnly
            Dim shouldClose = closeAfterSave
            saveTask = Nothing : saveDestination = Nothing : closeAfterSave = False
            RefreshModalState()
            Try
                Dim written = finished.GetAwaiter().GetResult()
                If Not exportOnly Then
                    document.Name = written.Name
                    documentPath = path
                    saved = written.Snapshot : history(historyIndex) = saved
                    UpdateDocument()
                End If
                SetStatus(If(exportOnly, "Exported ", "Saved ") & IO.Path.GetFileName(path))
                If continuePrompt Then CompletePrompt(True)
                If shouldClose Then RequestClose()
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                TypeOf ex Is InvalidDataException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is ArgumentException
                SetError("Could not save shader graph", ex.Message)
            End Try
        End Sub

        Private Function ChooseSave(filter As String, extension As String) As String
            Using dialog As New Forms.SaveFileDialog With {.Filter = filter, .DefaultExt = extension, .FileName = document.Name}
                Return If(dialog.ShowDialog(owner) = Forms.DialogResult.OK, dialog.FileName, Nothing)
            End Using
        End Function

        Private Sub ExportSource()
            ClearEditorFocus()
            Dim path = If(document.IsSurface, ChooseSave("Material source (*.hlsl)|*.hlsl", "hlsl"),
                ChooseSave("Effect source (*.fx)|*.fx", "fx"))
            If path Is Nothing Then Return
            Try
                File.WriteAllText(path, If(document.IsSurface, SurfaceMaterials.GenerateHlsl(document), ShaderGenerator.Generate(document)))
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
            If saveTask IsNot Nothing Then Return
            If presetBrowser.isVisible Then
                If key = Keys.Escape Then ShowPresetBrowser(False)
                Return
            End If
            If prompt.isVisible Then
                If key = Keys.Escape Then CompletePrompt(False)
                Return
            End If
            If Not (state.IsKeyDown(Keys.LeftControl) OrElse state.IsKeyDown(Keys.RightControl)) Then Return
            Select Case key
                Case Keys.N : Confirm(Sub() SetDocument(CreateNewDocument(), Nothing, keepSaved:=hosted))
                Case Keys.O : Confirm(AddressOf OpenDocument)
                Case Keys.P : ShowPresetBrowser(True)
                Case Keys.S
                    If hosted Then RunShader() Else StartSave()
                Case Keys.Enter : RunShader()
                Case Keys.Z : HistoryStep(-1)
                Case Keys.Y : HistoryStep(1)
            End Select
        End Sub

        Friend Sub CancelCompilation()
            If cancellation Is Nothing Then Return
            cancellation.Cancel()
            Try
                compile?.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult()
            Catch ex As Exception When TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is TimeoutException OrElse TypeOf ex Is IOException
                ' Closing the worker has no diagnostics panel to receive these results.
            Finally
                cancellation.Dispose()
            End Try
        End Sub

        Private Sub ClearEditorFocus()
            If document.Mode = ShaderMode.Advanced Then advanced.CommitEdits()
            FocusElement(Nothing)
        End Sub
    End Class
End Namespace
