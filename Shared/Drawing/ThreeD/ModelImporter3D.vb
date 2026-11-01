Imports System.IO

Namespace [Shared].Drawing.ThreeD

    ''' <summary>Contract implemented by the optional ProjectZ.ModelImport package.</summary>
    Public Interface IModelImporter3D
        Function Load(path As String) As Model3DAsset
    End Interface

    Public NotInheritable Class ModelImporter3D
        Private Shared ReadOnly supportedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".obj", ".fbx"}
        Private Shared ReadOnly importer As New Lazy(Of IModelImporter3D)(AddressOf CreateImporter)

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property SupportedFileExtensions As IReadOnlyCollection(Of String)
            Get
                Return supportedExtensions
            End Get
        End Property

        Public Shared Function Load(path As String) As Model3DAsset
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("A model path is required.", NameOf(path))
            Dim fullPath = IO.Path.GetFullPath(path)
            If Not File.Exists(fullPath) Then Throw New FileNotFoundException("3D model file was not found.", fullPath)
            If Not supportedExtensions.Contains(IO.Path.GetExtension(fullPath)) Then Throw New NotSupportedException("Project Z currently imports OBJ and FBX model files.")
            Return importer.Value.Load(fullPath)
        End Function

        Public Shared Function LoadAsync(path As String, Optional cancellationToken As Threading.CancellationToken = Nothing) As Task(Of Model3DAsset)
            Return Task.Run(Function()
                                cancellationToken.ThrowIfCancellationRequested()
                                Dim result = Load(path)
                                cancellationToken.ThrowIfCancellationRequested()
                                Return result
                            End Function, cancellationToken)
        End Function

        Private Shared Function CreateImporter() As IModelImporter3D
            Dim provider = Type.GetType("ProjectZ.ModelImport.AssimpModelImporter, ProjectZ.ModelImport", throwOnError:=False)
            If provider Is Nothing Then Throw New InvalidOperationException("Install the ProjectZ.ModelImport NuGet package to load OBJ and FBX files. Procedural 3D surfaces are available in the base ProjectZ package.")
            Return DirectCast(Activator.CreateInstance(provider), IModelImporter3D)
        End Function
    End Class
End Namespace
