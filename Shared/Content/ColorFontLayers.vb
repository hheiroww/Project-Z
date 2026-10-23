Imports System.Buffers.Binary
Imports System.IO
Imports Microsoft.Xna.Framework
Imports Media = System.Windows.Media

Namespace [Shared].Content
    ' OpenType COLR v0 layers (also present in COLR v1 fonts) and CPAL palette 0.
    ' Shaping, fallback, ligatures and glyph positions still come from WPF.
    Friend NotInheritable Class ColorFontLayers
        Friend Structure Layer
            Friend Glyph As UShort
            Friend Color As Color
            Friend Foreground As Boolean
        End Structure
        Private Shared ReadOnly cache As New Dictionary(Of String, ColorFontLayers)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly glyphs As New Dictionary(Of UShort, Layer())

        Friend Function ForGlyph(glyph As UShort) As Layer()
            Dim value As Layer() = Nothing
            glyphs.TryGetValue(glyph, value)
            Return value
        End Function

        Friend Shared Function ForTypeface(face As Media.GlyphTypeface) As ColorFontLayers
            If face.FontUri Is Nothing OrElse Not face.FontUri.IsFile Then Return Nothing
            Dim path = face.FontUri.LocalPath
            SyncLock cache
                Dim value As ColorFontLayers = Nothing
                If cache.TryGetValue(path, value) Then Return value
                Try
                    value = Load(path)
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                    TypeOf ex Is ArgumentException OrElse TypeOf ex Is OverflowException
                    value = Nothing ' Unsupported or malformed optional color data uses normal outlines.
                End Try
                If cache.Count >= 64 Then cache.Clear()
                cache.Add(path, value)
                Return value
            End SyncLock
        End Function

        Private Shared Function U16(data As Byte(), at As Integer) As UShort
            Return BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at, 2))
        End Function
        Private Shared Function U32(data As Byte(), at As Integer) As UInteger
            Return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at, 4))
        End Function
        Private Shared Function Read(stream As FileStream, offset As Long, length As Integer) As Byte()
            If length < 0 OrElse length > 16 * 1024 * 1024 OrElse offset < 0 OrElse offset > stream.Length - length Then Throw New InvalidDataException("Invalid optional font table bounds.")
            Dim data(length - 1) As Byte
            stream.Position = offset
            stream.ReadExactly(data)
            Return data
        End Function
        Private Shared Function Load(path As String) As ColorFontLayers
            Using stream As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                Dim header = Read(stream, 0, 12)
                If U32(header, 0) <> &H10000UI AndAlso U32(header, 0) <> &H4F54544FUI Then Return Nothing
                Dim directory = Read(stream, 12, CInt(U16(header, 4)) * 16)
                Dim colr As Byte() = Nothing, cpal As Byte() = Nothing
                For at = 0 To directory.Length - 16 Step 16
                    Dim tag = U32(directory, at)
                    If tag <> &H434F4C52UI AndAlso tag <> &H4350414CUI Then Continue For
                    Dim data = Read(stream, U32(directory, at + 8), CInt(U32(directory, at + 12)))
                    If tag = &H434F4C52UI Then colr = data Else cpal = data
                Next
                If colr Is Nothing OrElse cpal Is Nothing OrElse U16(colr, 0) > 1 OrElse U16(cpal, 0) > 1 OrElse U16(cpal, 4) = 0 Then Return Nothing
                Dim colorCount = CInt(U16(cpal, 6)), paletteStart = CInt(U16(cpal, 12))
                Dim entries = CInt(U16(cpal, 2)), colorOffset = CInt(U32(cpal, 8))
                If entries = 0 OrElse paletteStart + entries > colorCount OrElse colorOffset > cpal.Length - colorCount * 4 Then Return Nothing
                Dim colors(entries - 1) As Color
                For index = 0 To entries - 1
                    Dim at = colorOffset + (paletteStart + index) * 4
                    colors(index) = Color.FromNonPremultiplied(cpal(at + 2), cpal(at + 1), cpal(at), cpal(at + 3))
                Next
                Dim count = CInt(U16(colr, 2)), bases = CInt(U32(colr, 4)), layers = CInt(U32(colr, 8)), layerCount = CInt(U16(colr, 12))
                If bases > colr.Length - count * 6 OrElse layers > colr.Length - layerCount * 4 Then Return Nothing
                Dim result As New ColorFontLayers
                For index = 0 To count - 1
                    Dim at = bases + index * 6, glyph = U16(colr, at), first = CInt(U16(colr, at + 2)), size = CInt(U16(colr, at + 4))
                    If size = 0 OrElse first + size > layerCount Then Continue For
                    Dim records(size - 1) As Layer
                    For part = 0 To size - 1
                        Dim item = layers + (first + part) * 4, palette = CInt(U16(colr, item + 2))
                        If palette <> &HFFFF AndAlso palette >= colors.Length Then Throw New InvalidDataException("Invalid optional font palette index.")
                        records(part) = New Layer With {.Glyph = U16(colr, item), .Foreground = palette = &HFFFF, .Color = If(palette = &HFFFF, Color.White, colors(If(palette = &HFFFF, 0, palette)))}
                    Next
                    result.glyphs(glyph) = records
                Next
                Return If(result.glyphs.Count > 0, result, Nothing)
            End Using
        End Function
    End Class
End Namespace
