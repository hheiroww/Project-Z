Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Content
    ''' <summary>Optional application font/shaping backend. Return Nothing to use the built-in backend.</summary>
    Public Interface IVectorFontProvider
        Function FromFamily(familyName As String, weight As Integer, pointSize As Single) As IVectorFontFace
        Function FromFile(path As String, pointSize As Single) As IVectorFontFace
    End Interface

    ''' <summary>A font face owned and disposed by VectorFont. Layout uses logical pixels.
    ''' Draw on the game thread, honor GraphicsQuality.RenderScale, and restore changed GPU state.</summary>
    Public Interface IVectorFontFace
        Inherits IDisposable
        ReadOnly Property FamilyName As String
        ReadOnly Property FilePath As String
        ReadOnly Property LineSpacing As Integer
        Function Measure(text As String, pixelPerfect As Boolean) As Vector2
        Sub Draw(device As GraphicsDevice, text As String, position As Vector2, color As Color,
                 offset As Vector2, textScale As Single, pixelPerfect As Boolean)
    End Interface
End Namespace
