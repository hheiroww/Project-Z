Imports System.Collections.Generic
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Content
Imports System.Globalization

Namespace [Shared].Content

    Public Class ContentContainer
        'Implements IDisposable

        Private GraphicsDevice As GraphicsDevice

        Private ReadOnly ContentDirectoryOverride As String

        Public TexturesInstance As Textures

        Public Property Content As ContentManager

        Public Property Fonts As New Dictionary(Of String, SpriteFont)
        Public ReadOnly Property VectorFonts As New Dictionary(Of String, VectorFont)
        Private ReadOnly systemVectorFonts As New Dictionary(Of String, VectorFont)(StringComparer.Ordinal)
        Private ReadOnly systemFontOrder As New Queue(Of String)

        Public Shared Function SystemFontName(family As String, weight As Integer, pointSize As Single) As String
            If String.IsNullOrWhiteSpace(family) Then Throw New ArgumentException("Font family is required.", NameOf(family))
            If weight < 1 OrElse weight > 999 Then Throw New ArgumentOutOfRangeException(NameOf(weight))
            If Not Single.IsFinite(pointSize) OrElse pointSize <= 0 OrElse pointSize > 512 Then Throw New ArgumentOutOfRangeException(NameOf(pointSize))
            Return "SystemFont:" & Uri.EscapeDataString(family) & ":" & weight.ToString(CultureInfo.InvariantCulture) & ":" & pointSize.ToString(CultureInfo.InvariantCulture)
        End Function

        Private Shared ReadOnly DefaultVectorFontPath As String =
            IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf")

        Public Sub LoadVectorFont(name As String, fontFile As String, pointSize As Single)
            Dim font As New VectorFont(fontFile, pointSize)
            Dim previous As VectorFont = Nothing
            If VectorFonts.TryGetValue(name, previous) Then previous.Dispose()
            VectorFonts(name) = font
        End Sub

        ''' <summary>Loads the native font-file outlines without requiring Content/Fonts or compiled font assets.</summary>
        Public Sub LoadVectorFontsOnly()
            If Not IO.File.Exists(DefaultVectorFontPath) Then
                Throw New IO.FileNotFoundException("The system Segoe UI font is unavailable. Register a font file with LoadVectorFont instead.", DefaultVectorFontPath)
            End If
            For Each size In {10, 12, 14, 18, 24, 36, 48, 72}
                Dim font As VectorFont = Nothing
                TryGetVectorFont("SegoeUI_" & size, font)
            Next
        End Sub

        ''' <summary>Resolves registered outlines and system Segoe UI sizes on demand, without a SpriteFont lookup.</summary>
        Public Function TryGetVectorFont(name As String, <System.Runtime.InteropServices.Out> ByRef font As VectorFont) As Boolean
            font = Nothing
            If String.IsNullOrEmpty(name) Then Return False
            If VectorFonts.TryGetValue(name, font) Then Return True
            If name.StartsWith("SystemFont:", StringComparison.Ordinal) Then
                If systemVectorFonts.TryGetValue(name, font) Then Return True
                Dim parts = name.Split(":"c)
                Dim weight As Integer, points As Single
                If parts.Length <> 4 OrElse Not Integer.TryParse(parts(2), NumberStyles.Integer, CultureInfo.InvariantCulture, weight) OrElse
                    Not Single.TryParse(parts(3), NumberStyles.Float, CultureInfo.InvariantCulture, points) Then Return False
                ' Bound generated font variants and release their GPU meshes and device subscriptions.
                While systemVectorFonts.Count >= 64
                    Dim oldest = systemFontOrder.Dequeue()
                    systemVectorFonts(oldest).Dispose()
                    systemVectorFonts.Remove(oldest)
                End While
                font = New VectorFont(Uri.UnescapeDataString(parts(1)), weight, points)
                systemVectorFonts.Add(name, font)
                systemFontOrder.Enqueue(name)
                Return True
            End If
            Const prefix As String = "SegoeUI_"
            If Not name.StartsWith(prefix, StringComparison.Ordinal) Then Return False
            Dim size As Single
            If Not Single.TryParse(name.Substring(prefix.Length), NumberStyles.AllowDecimalPoint,
                                   CultureInfo.InvariantCulture, size) OrElse
               Not Single.IsFinite(size) OrElse size <= 0 OrElse size > 512 OrElse
               Not IO.File.Exists(DefaultVectorFontPath) Then Return False
            LoadVectorFont(name, DefaultVectorFontPath, size)
            font = VectorFonts(name)
            Return True
        End Function

        Public Property Textures As New Dictionary(Of String, Texture2D)

        Public Property AllCollections As New List(Of Collections.IDictionary) From {Fonts, Textures}


#Region "Content Methods"

        Public Sub LoadAllContent()
            If IO.File.Exists(DefaultVectorFontPath) Then LoadVectorFontsOnly()
            '#If WINDOWS Then
            ' Content roots must follow the host that owns this ContentManager.
            ' VST hosts run from the DAW directory, so AppDomain.BaseDirectory
            ' points at FL Studio rather than at the plugin installation.
            Dim ContentDirectory As String = ContentDirectoryOverride
            If String.IsNullOrWhiteSpace(ContentDirectory) Then
                ContentDirectory = Content.RootDirectory
            End If
            If Not IO.Path.IsPathRooted(ContentDirectory) Then
                ContentDirectory = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ContentDirectory)
            End If
            ContentDirectory = IO.Path.GetFullPath(ContentDirectory)

            Dim FontsDirectory As String = IO.Path.Combine(ContentDirectory, "Fonts")
            If Not IO.Directory.Exists(FontsDirectory) Then
                If VectorFonts.Count > 0 Then Return
                Throw New IO.DirectoryNotFoundException(
                    $"Project Z content directory does not contain Fonts: '{FontsDirectory}'.")
            End If

            For Each FontDirectory As String In IO.Directory.GetDirectories(FontsDirectory)
                For Each FontPath As String In IO.Directory.GetFiles(FontDirectory)
                    Dim RelativeFontPath As String = FontPath.Remove(0, ContentDirectory.Length + 1)
                    Dim Extension As String = IO.Path.GetExtension(RelativeFontPath)
                    Dim Font As SpriteFont = Content.Load(Of SpriteFont)(RelativeFontPath.Replace(Extension, Nothing))
                    Fonts.Add(IO.Path.GetFileNameWithoutExtension(FontPath), Font)
                Next
            Next
            '#End If
        End Sub
#If WINDOWS Then
        Public Function GetContentResource(Name As String) As Byte()
            Return DirectCast(My.Resources.ResourceManager.GetObject(Name), Byte())
        End Function
#End If
#End Region

        Public Sub New(Content As ContentManager, GraphicsDevice As GraphicsDevice,
                       Optional ContentDirectory As String = Nothing)
            Me.Content = Content
            Me.GraphicsDevice = GraphicsDevice
            ContentDirectoryOverride = ContentDirectory
            TexturesInstance = New Textures(GraphicsDevice)
        End Sub

        '#Region "IDisposable Support"
        '        Private disposedValue As Boolean ' To detect redundant calls

        '        ' IDisposable
        '        Protected Overridable Sub Dispose(disposing As Boolean)
        '            If Not disposedValue Then
        '                If disposing Then
        '                    ' TODO: dispose managed state (managed objects).
        '                    For Each Texture As Texture2D In Textures.Values
        '                        Texture.Dispose()
        '                    Next
        '                    Content.Dispose()
        '                End If

        '                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
        '                ' TODO: set large fields to null.
        '                Fonts = Nothing
        '                Textures = Nothing
        '                AllCollections = Nothing
        '            End If
        '            disposedValue = True
        '        End Sub

        '        ' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        '        'Protected Overrides Sub Finalize()
        '        '    ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        '        '    Dispose(False)
        '        '    MyBase.Finalize()
        '        'End Sub

        '        ' This code added by Visual Basic to correctly implement the disposable pattern.
        '        Public Sub Dispose() Implements IDisposable.Dispose
        '            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        '            Dispose(True)
        '            GC.SuppressFinalize(Me)
        '        End Sub
        '#End Region

    End Class

End Namespace
