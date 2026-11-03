Imports System.IO
Imports System.Text
Imports Microsoft.Xna.Framework

Namespace [Shared].Drawing.Particles
    Friend NotInheritable Class DecodedGifFrame
        Public Width As Integer
        Public Height As Integer
        Public Pixels As Color()
        Public Seconds As Double
    End Class

    ''' <summary>Small bounded GIF87a/89a decoder for particle atlases, including local palettes and disposal.</summary>
    Friend NotInheritable Class GifSpriteDecoder
        Public Shared Function Decode(stream As Stream) As List(Of DecodedGifFrame)
            Using reader As New BinaryReader(stream, Encoding.ASCII, leaveOpen:=True)
                Dim signature = Encoding.ASCII.GetString(ReadExact(reader, 6))
                If signature <> "GIF87a" AndAlso signature <> "GIF89a" Then Throw New InvalidDataException("Expected a GIF image.")
                Dim width = CInt(reader.ReadUInt16()), height = CInt(reader.ReadUInt16())
                If width < 1 OrElse height < 1 OrElse width > 4096 OrElse height > 4096 OrElse CLng(width) * height > 16777216 Then Throw New InvalidDataException("GIF canvas exceeds particle atlas limits.")
                Dim packed = reader.ReadByte(), backgroundIndex = CInt(reader.ReadByte())
                reader.ReadByte()
                Dim palette As Color() = If((packed And &H80) <> 0, ReadPalette(reader, 1 << ((packed And 7) + 1)), Array.Empty(Of Color)())
                Dim canvas(width * height - 1) As Color
                Dim frames As New List(Of DecodedGifFrame)
                Dim transparentIndex As Integer = -1, disposal As Integer = 0
                Dim delay As Double = 0.1
                While True
                    Dim marker = reader.ReadByte()
                    If marker = &H3B Then Exit While
                    If marker = &H21 Then
                        Dim label = reader.ReadByte()
                        If label = &HF9 Then
                            If reader.ReadByte() <> 4 Then Throw New InvalidDataException("Malformed GIF graphics control.")
                            Dim control = reader.ReadByte()
                            disposal = (control >> 2) And 7
                            delay = Math.Max(0.01, reader.ReadUInt16() / 100.0)
                            Dim index = CInt(reader.ReadByte())
                            transparentIndex = If((control And 1) <> 0, index, -1)
                            If reader.ReadByte() <> 0 Then Throw New InvalidDataException("Malformed GIF control terminator.")
                        Else
                            ReadSubBlocks(reader)
                        End If
                        Continue While
                    End If
                    If marker <> &H2C Then Throw New InvalidDataException("Unexpected GIF block.")
                    Dim left = CInt(reader.ReadUInt16()), top = CInt(reader.ReadUInt16())
                    Dim frameWidth = CInt(reader.ReadUInt16()), frameHeight = CInt(reader.ReadUInt16())
                    If frameWidth < 1 OrElse frameHeight < 1 OrElse left + frameWidth > width OrElse top + frameHeight > height Then Throw New InvalidDataException("GIF frame lies outside its canvas.")
                    Dim descriptor = reader.ReadByte()
                    Dim localPalette = If((descriptor And &H80) <> 0, ReadPalette(reader, 1 << ((descriptor And 7) + 1)), palette)
                    If localPalette.Length = 0 Then Throw New InvalidDataException("GIF frame has no palette.")
                    Dim minimumCodeSize = CInt(reader.ReadByte())
                    Dim indices = DecodeLzw(ReadSubBlocks(reader), minimumCodeSize, frameWidth * frameHeight)
                    Dim previous = If(disposal = 3, DirectCast(canvas.Clone(), Color()), Nothing)
                    Dim sourceRow = 0
                    If (descriptor And &H40) <> 0 Then
                        Dim starts As Integer() = {0, 4, 2, 1}, increments As Integer() = {8, 8, 4, 2}
                        For pass = 0 To 3
                            For row = starts(pass) To frameHeight - 1 Step increments(pass)
                                CopyRow(indices, sourceRow, canvas, row, frameWidth, width, left, top, localPalette, transparentIndex)
                                sourceRow += 1
                            Next
                        Next
                    Else
                        For row = 0 To frameHeight - 1
                            CopyRow(indices, row, canvas, row, frameWidth, width, left, top, localPalette, transparentIndex)
                        Next
                    End If
                    If frames.Count >= 256 OrElse CLng(frames.Count + 1) * width * height > 16777216 Then Throw New InvalidDataException("GIF exceeds 256 frames or 16 million decoded pixels.")
                    frames.Add(New DecodedGifFrame With {.Width = width, .Height = height, .Pixels = DirectCast(canvas.Clone(), Color()), .Seconds = delay})
                    If disposal = 2 Then
                        Dim background = If(transparentIndex >= 0 OrElse backgroundIndex >= palette.Length, Color.Transparent, palette(backgroundIndex))
                        For row = top To top + frameHeight - 1
                            For column = left To left + frameWidth - 1
                                canvas(row * width + column) = background
                            Next
                        Next
                    ElseIf disposal = 3 Then
                        canvas = previous
                    End If
                    transparentIndex = -1 : disposal = 0 : delay = 0.1
                End While
                If frames.Count = 0 Then Throw New InvalidDataException("GIF contains no image frames.")
                Return frames
            End Using
        End Function

        Private Shared Sub CopyRow(indices As Byte(), sourceRow As Integer, canvas As Color(), targetRow As Integer, frameWidth As Integer, width As Integer, left As Integer, top As Integer, palette As Color(), transparent As Integer)
            For column = 0 To frameWidth - 1
                Dim index = CInt(indices(sourceRow * frameWidth + column))
                If index = transparent Then Continue For
                If index >= palette.Length Then Throw New InvalidDataException("GIF palette index is invalid.")
                canvas((top + targetRow) * width + left + column) = palette(index)
            Next
        End Sub

        Private Shared Function ReadPalette(reader As BinaryReader, count As Integer) As Color()
            Dim result(count - 1) As Color
            For index = 0 To count - 1
                result(index) = New Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), CByte(255))
            Next
            Return result
        End Function
        Private Shared Function ReadExact(reader As BinaryReader, count As Integer) As Byte()
            Dim result = reader.ReadBytes(count)
            If result.Length <> count Then Throw New EndOfStreamException()
            Return result
        End Function
        Private Shared Function ReadSubBlocks(reader As BinaryReader) As Byte()
            Using data As New MemoryStream
                While True
                    Dim size = CInt(reader.ReadByte())
                    If size = 0 Then Return data.ToArray()
                    If data.Length + size > 67108864 Then Throw New InvalidDataException("GIF compressed data exceeds 64 MB.")
                    Dim block = ReadExact(reader, size)
                    data.Write(block, 0, block.Length)
                End While
            End Using
            Throw New InvalidDataException("Missing GIF block terminator.")
        End Function

        Private Shared Function DecodeLzw(data As Byte(), minimumBits As Integer, pixelCount As Integer) As Byte()
            If minimumBits < 2 OrElse minimumBits > 8 Then Throw New InvalidDataException("Invalid GIF LZW code size.")
            Dim prefix(4095) As Integer, suffix(4095) As Byte, stack(4096) As Byte
            Dim clear = 1 << minimumBits, stopCode = clear + 1, nextCode = clear + 2, codeBits = minimumBits + 1
            For index = 0 To clear - 1
                suffix(index) = CByte(index)
            Next
            Dim output(pixelCount - 1) As Byte
            Dim outputIndex = 0, bitPosition = 0, oldCode = -1, first As Byte = 0
            While bitPosition + codeBits <= CLng(data.Length) * 8
                Dim code = 0
                For bit = 0 To codeBits - 1
                    If (data(bitPosition >> 3) And (1 << (bitPosition And 7))) <> 0 Then code = code Or (1 << bit)
                    bitPosition += 1
                Next
                If code = clear Then
                    nextCode = clear + 2 : codeBits = minimumBits + 1 : oldCode = -1
                    Continue While
                End If
                If code = stopCode Then Exit While
                If code > nextCode OrElse code >= 4096 Then Throw New InvalidDataException("Invalid GIF LZW code.")
                Dim original = code, stackCount = 0
                If code = nextCode Then
                    If oldCode < 0 Then Throw New InvalidDataException("Invalid initial GIF LZW code.")
                    stack(stackCount) = first : stackCount += 1 : code = oldCode
                End If
                While code >= clear
                    If code >= nextCode OrElse stackCount >= 4096 Then Throw New InvalidDataException("Invalid GIF LZW dictionary chain.")
                    stack(stackCount) = suffix(code) : stackCount += 1 : code = prefix(code)
                End While
                first = suffix(code)
                stack(stackCount) = first : stackCount += 1
                For index = stackCount - 1 To 0 Step -1
                    If outputIndex >= pixelCount Then Throw New InvalidDataException("GIF contains too many decoded pixels.")
                    output(outputIndex) = stack(index) : outputIndex += 1
                Next
                If oldCode >= 0 AndAlso nextCode < 4096 Then
                    prefix(nextCode) = oldCode : suffix(nextCode) = first : nextCode += 1
                    If nextCode = (1 << codeBits) AndAlso codeBits < 12 Then codeBits += 1
                End If
                oldCode = original
            End While
            If outputIndex <> pixelCount Then Throw New InvalidDataException("GIF frame has incomplete pixel data.")
            Return output
        End Function
    End Class
End Namespace
