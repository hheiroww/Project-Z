Imports System.IO
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing.Particles
    Public NotInheritable Class ParticleSpriteFrame
        Public ReadOnly Property Texture As Texture2D
        Public ReadOnly Property Source As Rectangle
        Public ReadOnly Property Duration As Double
        Friend Sub New(texture As Texture2D, source As Rectangle, duration As Double)
            Me.Texture = texture : Me.Source = source : Me.Duration = duration
        End Sub
    End Class

    ''' <summary>Reusable immutable frame definitions with explicit texture ownership. Create/load on the graphics thread.</summary>
    Public NotInheritable Class ParticleSpriteAnimation
        Implements IDisposable
        Private ReadOnly framesValue As New List(Of ParticleSpriteFrame)
        Private ReadOnly ownedTextures As New HashSet(Of Texture2D)
        Private endTimes As Double() = Array.Empty(Of Double)()
        Private totalDuration As Double
        Private disposed As Boolean

        Public ReadOnly Property Frames As IReadOnlyList(Of ParticleSpriteFrame)
            Get
                Return framesValue.AsReadOnly()
            End Get
        End Property
        Public Property LoopAnimation As Boolean = True
        Public ReadOnly Property Duration As Double
            Get
                Return totalDuration
            End Get
        End Property

        ''' <summary>Caller textures remain caller-owned unless ownsTexture is explicitly true.</summary>
        Public Sub AddFrame(texture As Texture2D, source As Rectangle, Optional seconds As Double = 0.1, Optional ownsTexture As Boolean = False)
            ObjectDisposedException.ThrowIf(disposed, Me)
            If texture Is Nothing OrElse texture.IsDisposed Then Throw New ArgumentException("A live texture is required.", NameOf(texture))
            If source.Width <= 0 OrElse source.Height <= 0 OrElse source.X < 0 OrElse source.Y < 0 OrElse CLng(source.X) + source.Width > texture.Width OrElse CLng(source.Y) + source.Height > texture.Height Then Throw New ArgumentOutOfRangeException(NameOf(source))
            If Not Double.IsFinite(seconds) OrElse seconds <= 0 OrElse seconds > 3600 Then Throw New ArgumentOutOfRangeException(NameOf(seconds))
            If framesValue.Count >= 4096 Then Throw New InvalidOperationException("An animation supports at most 4096 managed frames.")
            framesValue.Add(New ParticleSpriteFrame(texture, source, seconds))
            If ownsTexture Then ownedTextures.Add(texture)
            totalDuration += seconds
            ReDim Preserve endTimes(framesValue.Count - 1)
            endTimes(endTimes.Length - 1) = totalDuration
        End Sub

        Public Function FrameAt(ageSeconds As Double) As ParticleSpriteFrame
            ObjectDisposedException.ThrowIf(disposed, Me)
            If framesValue.Count = 0 Then Return Nothing
            Return framesValue(FrameIndexAt(endTimes, ageSeconds, LoopAnimation))
        End Function

        ''' <summary>Selects variable-duration frames; available separately for managed timeline testing.</summary>
        Public Shared Function FrameIndexAt(frameEndTimes As IReadOnlyList(Of Double), ageSeconds As Double, Optional loopFrames As Boolean = True) As Integer
            If frameEndTimes Is Nothing OrElse frameEndTimes.Count = 0 Then Return -1
            Dim duration = frameEndTimes(frameEndTimes.Count - 1)
            If Not Double.IsFinite(duration) OrElse duration <= 0 Then Throw New ArgumentException("Frame end times must have a positive finite total duration.", NameOf(frameEndTimes))
            Dim time = If(Double.IsFinite(ageSeconds), Math.Max(ageSeconds, 0), 0)
            If loopFrames Then
                time = time Mod duration
            ElseIf time >= duration Then
                Return frameEndTimes.Count - 1
            End If
            Dim low = 0, high = frameEndTimes.Count - 1
            While low < high
                Dim middle = (low + high) \ 2
                If time < frameEndTimes(middle) Then
                    high = middle
                Else
                    low = middle + 1
                End If
            End While
            Return low
        End Function

        Public Shared Function LoadPng(device As GraphicsDevice, path As String) As ParticleSpriteAnimation
            Using stream = File.OpenRead(path)
                Dim texture = Texture2D.FromStream(device, stream)
                Dim result As New ParticleSpriteAnimation
                Try
                    result.AddFrame(texture, New Rectangle(0, 0, texture.Width, texture.Height), 1, True)
                    Return result
                Catch
                    texture.Dispose() : Throw
                End Try
            End Using
        End Function

        ''' <summary>Uniform PNG atlas in row-major order. frameCount=0 uses every complete cell.</summary>
        Public Shared Function LoadSpriteSheet(device As GraphicsDevice, path As String, frameWidth As Integer, frameHeight As Integer, Optional secondsPerFrame As Double = 0.1, Optional frameCount As Integer = 0) As ParticleSpriteAnimation
            If frameWidth <= 0 OrElse frameHeight <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(frameWidth))
            Using stream = File.OpenRead(path)
                Dim texture = Texture2D.FromStream(device, stream)
                Dim result As New ParticleSpriteAnimation
                Try
                    Dim columns = texture.Width \ frameWidth, rows = texture.Height \ frameHeight
                    Dim available = CLng(columns) * rows
                    If available < 1 OrElse available > 4096 Then Throw New ArgumentException("The sheet must contain 1 through 4096 complete cells.")
                    If frameCount = 0 Then frameCount = CInt(available)
                    If frameCount < 1 OrElse frameCount > available Then Throw New ArgumentOutOfRangeException(NameOf(frameCount))
                    For index = 0 To frameCount - 1
                        result.AddFrame(texture, New Rectangle((index Mod columns) * frameWidth, (index \ columns) * frameHeight, frameWidth, frameHeight), secondsPerFrame, True)
                    Next
                    Return result
                Catch
                    result.Dispose()
                    If Not texture.IsDisposed Then texture.Dispose()
                    Throw
                End Try
            End Using
        End Function

        ''' <summary>Decodes GIF89a disposal/interlace/variable delays into one GPU atlas without OS image APIs.</summary>
        Public Shared Function LoadGif(device As GraphicsDevice, path As String) As ParticleSpriteAnimation
            Using stream = File.OpenRead(path)
                Dim decoded = GifSpriteDecoder.Decode(stream)
                Dim width = decoded(0).Width, height = decoded(0).Height
                Dim columns = Math.Min(4096 \ width, decoded.Count)
                Dim rows = (decoded.Count + columns - 1) \ columns
                If rows * height > 4096 Then Throw New InvalidDataException("GIF frames exceed the 4096 by 4096 particle atlas limit; use a smaller animation.")
                Dim atlasWidth = columns * width, atlasHeight = rows * height
                Dim pixels(atlasWidth * atlasHeight - 1) As Color
                For index = 0 To decoded.Count - 1
                    Dim x = (index Mod columns) * width, y = (index \ columns) * height
                    For row = 0 To height - 1
                        Array.Copy(decoded(index).Pixels, row * width, pixels, (y + row) * atlasWidth + x, width)
                    Next
                Next
                Dim texture As New Texture2D(device, atlasWidth, atlasHeight)
                Dim result As New ParticleSpriteAnimation
                Try
                    texture.SetData(pixels)
                    For index = 0 To decoded.Count - 1
                        result.AddFrame(texture, New Rectangle((index Mod columns) * width, (index \ columns) * height, width, height), decoded(index).Seconds, True)
                    Next
                    Return result
                Catch
                    result.Dispose()
                    If Not texture.IsDisposed Then texture.Dispose()
                    Throw
                End Try
            End Using
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            For Each texture In ownedTextures
                texture.Dispose()
            Next
            ownedTextures.Clear() : framesValue.Clear() : endTimes = Array.Empty(Of Double)()
        End Sub
    End Class
End Namespace
