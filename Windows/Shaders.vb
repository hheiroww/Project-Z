Imports System.Text.Json
Imports ProjectZ.ShaderEditor
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition
Imports ProjectZ.Windows.ShaderEditing

''' <summary>Runtime authoring for Project-Z XAML visual effects.</summary>
Public NotInheritable Class Shaders
    Private Sub New()
    End Sub

    ''' <summary>Opens a modeless shader editor. Run compiles an effect for this application's graphics backend.</summary>
    Public Shared Function ShowEditor(Optional documentPath As String = Nothing, Optional appearance As WindowAppearance = Nothing) As ShaderEditorSession
        Dim path = If(String.IsNullOrWhiteSpace(documentPath), Nothing, IO.Path.GetFullPath(documentPath))
        Return ShowEditorCore(path, Nothing, appearance)
    End Function

    ''' <summary>Opens a validated graph as a draft with no save path.</summary>
    Public Shared Function ShowEditorForGraph(graphJson As String, Optional appearance As WindowAppearance = Nothing) As ShaderEditorSession
        ShaderDocument.Parse(graphJson)
        Return ShowEditorCore(Nothing, graphJson, appearance)
    End Function

    ''' <summary>Opens the editor for a video host that commits graphs after native validation.</summary>
    Public Shared Function ShowEditorForVideoGraph(graphJson As String, Optional appearance As WindowAppearance = Nothing) As ShaderEditorSession
        If graphJson IsNot Nothing AndAlso ShaderDocument.Parse(graphJson).IsSurface Then
            Throw New IO.InvalidDataException("A video clip shader needs a pixel-effect graph.")
        End If
        Return ShowEditorCore(Nothing, graphJson, appearance, hosted:=True)
    End Function

    ''' <summary>Opens the 3D material editor. A null graph starts with Clear Water.</summary>
    Public Shared Function ShowSurfaceEditorForGraph(graphJson As String, Optional appearance As WindowAppearance = Nothing) As ShaderEditorSession
        If graphJson IsNot Nothing AndAlso Not ShaderDocument.Parse(graphJson).IsSurface Then
            Throw New IO.InvalidDataException("A surface needs a material graph.")
        End If
        Return ShowEditorCore(Nothing, graphJson, appearance, hosted:=True, surface:=True)
    End Function

    ''' <summary>Opens the managed video graph editor as a modeless window owned by the host HWND.</summary>
    Public Shared Function ShowOwnedVideoShaderEditor(graphJson As String, appearance As WindowAppearance, ownerHandle As IntPtr, title As String) As ShaderEditorSession
        If ownerHandle = IntPtr.Zero Then Throw New ArgumentException("A host window is required.", NameOf(ownerHandle))
        If graphJson IsNot Nothing AndAlso ShaderDocument.Parse(graphJson).IsSurface Then
            Throw New IO.InvalidDataException("A video clip shader needs a pixel-effect graph.")
        End If
        Return ShowEditorCore(Nothing, graphJson, appearance, hosted:=True, ownerHandle:=ownerHandle, title:=title)
    End Function

    ''' <summary>Opens the material editor as a modeless window owned by the host HWND.</summary>
    Public Shared Function ShowOwnedSurfaceShaderEditor(graphJson As String, appearance As WindowAppearance, ownerHandle As IntPtr, title As String) As ShaderEditorSession
        If ownerHandle = IntPtr.Zero Then Throw New ArgumentException("A host window is required.", NameOf(ownerHandle))
        If graphJson IsNot Nothing AndAlso Not ShaderDocument.Parse(graphJson).IsSurface Then
            Throw New IO.InvalidDataException("A surface needs a material graph.")
        End If
        Return ShowEditorCore(Nothing, graphJson, appearance, hosted:=True, surface:=True, ownerHandle:=ownerHandle, title:=title)
    End Function

    Private Shared Function ShowEditorCore(documentPath As String, graphJson As String, appearance As WindowAppearance,
                                           Optional hosted As Boolean = False, Optional surface As Boolean = False,
                                           Optional ownerHandle As IntPtr = Nothing, Optional title As String = Nothing) As ShaderEditorSession
#If PROJECTZ_OPENGL Then
        Throw New PlatformNotSupportedException("The shader editor requires a DirectX build of Project-Z.")
#Else
        Dim theme = If(appearance, WindowAppearance.Default)
        Dim host As New ShaderWorkerProcess(documentPath, JsonSerializer.Serialize(theme.Settings), graphJson, hosted, surface, ownerHandle.ToInt64(), title)
        Dim handler As EventHandler = Sub(sender, args) host.RefreshAppearance(JsonSerializer.Serialize(theme.Settings))
        AddHandler theme.Changed, handler
        host.RefreshAppearance(JsonSerializer.Serialize(theme.Settings))
        Dim detach = host.Completion.ContinueWith(Sub(unused) RemoveHandler theme.Changed, handler,
            Threading.CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
        Return New ShaderEditorSession(host)
#End If
    End Function

    ' Called by the editor worker in its own process. Keep MonoGame's SDL and graphics globals local to that process.
    Private Shared Sub RunEditorWorker(documentPath As String, graphJson As String, appearanceJson As Func(Of String), publish As Action(Of Byte(), String),
                                       shouldClose As Func(Of Boolean), takeReceipt As Func(Of ShaderGraphReceipt), hosted As Boolean, surface As Boolean)
        RunOwnedEditorWorker(documentPath, graphJson, appearanceJson, publish, shouldClose, takeReceipt, hosted, surface,
                             0, Nothing, Sub(handle) Return, Function() False)
    End Sub

    Private Shared Sub RunOwnedEditorWorker(documentPath As String, graphJson As String, appearanceJson As Func(Of String), publish As Action(Of Byte(), String),
                                            shouldClose As Func(Of Boolean), takeReceipt As Func(Of ShaderGraphReceipt), hosted As Boolean, surface As Boolean,
                                            ownerHandle As Long, title As String, ready As Action(Of Long), takeActivation As Func(Of Boolean))
        If shouldClose() Then Return
        Dim snapshot As String = Nothing
        Dim current As WindowAppearanceSettings = Nothing
        Dim getAppearance As Func(Of WindowAppearanceSettings) =
            Function()
                Dim nextSnapshot = appearanceJson()
                If Not String.Equals(snapshot, nextSnapshot, StringComparison.Ordinal) Then
                    current = If(JsonSerializer.Deserialize(Of WindowAppearanceSettings)(nextSnapshot), New WindowAppearanceSettings())
                    snapshot = nextSnapshot
                End If
                Return current
            End Function
        Using game As New ShaderEditorGame(documentPath, graphJson, getAppearance, publish, shouldClose, takeReceipt, hosted, surface,
                                          New IntPtr(ownerHandle), title, ready, takeActivation)
            game.Run()
        End Using
    End Sub
End Class

Public NotInheritable Class ShaderEditorSession
    Private ReadOnly host As ShaderWorkerProcess

    Friend Sub New(host As ShaderWorkerProcess)
        Me.host = host
    End Sub

    ''' <summary>Completes when the editor closes, or reports an error opening the window.</summary>
    Public ReadOnly Property Completion As Task
        Get
            Return host.Completion
        End Get
    End Property

    ''' <summary>Gets the visible editor HWND after its first frame. Zero means it is starting or closed.</summary>
    Public ReadOnly Property WindowHandle As IntPtr
        Get
            Return New IntPtr(host.WindowHandle)
        End Get
    End Property

    ''' <summary>Restores and activates an existing editor on behalf of a host action.</summary>
    Public Sub Activate()
        host.Activate()
    End Sub

    ''' <summary>Consumes the newest successful Run. Call from the application's update or render thread.</summary>
    Public Function TryTakeEffect(ByRef effect As XamlNativeEffect) As Boolean
        Dim bytecode = host.TakeLatestBytecode()
        If bytecode Is Nothing Then Return False
        effect = XamlNativeEffect.FromCompiledShader(bytecode)
        Return True
    End Function

    ''' <summary>Consumes the graph for the newest successful Run, independently of the compiled effect.</summary>
    Public Function TryTakeGraph(ByRef graphJson As String) As Boolean
        Dim graph = host.TakeLatestGraph()
        If graph Is Nothing Then Return False
        graphJson = graph
        Return True
    End Function

    ''' <summary>Confirms the host committed the exact graph returned by TryTakeGraph.</summary>
    Public Sub AcknowledgeGraph(graphJson As String)
        host.ConfirmGraph(graphJson, Nothing)
    End Sub

    ''' <summary>Leaves a graph editable when host validation or application fails.</summary>
    Public Sub RejectGraph(graphJson As String, message As String)
        host.ConfirmGraph(graphJson, If(String.IsNullOrWhiteSpace(message), "The host could not apply this graph.", message))
    End Sub

    Public Sub Close()
        host.Close()
    End Sub
End Class
