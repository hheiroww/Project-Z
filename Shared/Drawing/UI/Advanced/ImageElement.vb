Imports System.IO
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing.UI.Advanced
    Public Enum ImageStretch
        None
        Fill
        Uniform
        UniformToFill
    End Enum

    ''' <summary>
    ''' A single-image Project-Z control with file/stream loading and the common
    ''' WPF stretch modes. Texture replacement happens on the render thread.
    ''' </summary>
    Public Class ImageElement
        Inherits SceneElement

        Private _texture As Texture2D
        Private _ownsTexture As Boolean
        Public Property Source As String = String.Empty
        Public Property Stretch As ImageStretch = ImageStretch.Uniform
        Public Property Tint As Color = Color.White
        Public Property OpacityMaskBrush As Primitives.XamlGradientBrush
        Public Property MaskCornerRadius As Func(Of Single)
        Public Property MaskCornerRadii As Func(Of Primitives.CornerRadii)
        Public Property MaskBackgroundColor As Func(Of Color)
        Private maskBrush As Primitives.XamlGradientBrush
        Private maskedTexture As Texture2D
        Private maskedSource As Texture2D
        Private maskRevision As Integer = -1
        Private originalPixels As Color()
        Private maskedPixels As Color()
        Private maskPixels As Color()
        Private previousMaskCrop As Rectangle
        Private sourcePixelsDirty As Boolean

        Protected Sub InvalidateTexturePixels()
            sourcePixelsDirty = True
            maskRevision = -1
        End Sub

        Public Property Texture As Texture2D
            Get
                Return _texture
            End Get
            Set(value As Texture2D)
                If _ownsTexture AndAlso _texture IsNot Nothing Then _texture.Dispose()
                _texture = value
                _ownsTexture = False
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            Clip = True
        End Sub

        Public Sub Load(path As String)
            Source = path
            Using stream = File.OpenRead(path)
                Load(stream)
            End Using
        End Sub

        Public Sub Load(stream As Stream)
            If _ownsTexture AndAlso _texture IsNot Nothing Then _texture.Dispose()
            _texture = Texture2D.FromStream(Scene.graphicsDevice, stream)
            _ownsTexture = True
            If Size = Vector2.Zero Then Size = New Vector2(_texture.Width, _texture.Height)
        End Sub

        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            If _texture Is Nothing Then Return
            Dim drawTexture = _texture
            Dim destination = Rectangle
            Dim source As Rectangle? = Nothing
            Dim sourceAspect = CSng(_texture.Width) / Math.Max(1, _texture.Height)
            Dim targetAspect = CSng(Math.Max(1, Rectangle.Width)) / Math.Max(1, Rectangle.Height)

            Select Case Stretch
                Case ImageStretch.None
                    destination = New Rectangle(Rectangle.X, Rectangle.Y, _texture.Width, _texture.Height)
                Case ImageStretch.Uniform
                    Dim useWidth = targetAspect < sourceAspect
                    Dim width As Integer
                    Dim height As Integer
                    If useWidth Then
                        width = Rectangle.Width
                        height = CInt(width / sourceAspect)
                    Else
                        height = Rectangle.Height
                        width = CInt(height * sourceAspect)
                    End If
                    destination = New Rectangle(Rectangle.X + (Rectangle.Width - width) \ 2,
                                                Rectangle.Y + (Rectangle.Height - height) \ 2,
                                                width, height)
                Case ImageStretch.UniformToFill
                    ' Crop the source texture into the destination rectangle.
                    ' Enlarging the destination (the old behavior) let pixels
                    ' escape card/avatar bounds whenever scissoring was absent.
                    If sourceAspect > targetAspect Then
                        Dim cropWidth = Math.Max(1, CInt(_texture.Height * targetAspect))
                        source = New Rectangle((_texture.Width - cropWidth) \ 2, 0, cropWidth, _texture.Height)
                    ElseIf sourceAspect < targetAspect Then
                        Dim cropHeight = Math.Max(1, CInt(_texture.Width / targetAspect))
                        source = New Rectangle(0, (_texture.Height - cropHeight) \ 2, _texture.Width, cropHeight)
                    End If
            End Select

            If OpacityMaskBrush IsNot Nothing OrElse MaskBackgroundColor IsNot Nothing Then
                If maskBrush Is Nothing Then
                    maskBrush = New Primitives.XamlGradientBrush()
                End If
                If OpacityMaskBrush IsNot Nothing Then
                    maskBrush.Stops.Clear()
                    maskBrush.Stops.AddRange(OpacityMaskBrush.Stops)
                    maskBrush.IsRadial = OpacityMaskBrush.IsRadial
                    maskBrush.StartPoint = OpacityMaskBrush.StartPoint
                    maskBrush.EndPoint = OpacityMaskBrush.EndPoint
                    maskBrush.Center = OpacityMaskBrush.Center
                    maskBrush.Radius = OpacityMaskBrush.Radius
                    maskBrush.GradientOrigin = OpacityMaskBrush.GradientOrigin
                    maskBrush.Opacity = OpacityMaskBrush.Opacity
                    maskBrush.AbsoluteMapping = OpacityMaskBrush.AbsoluteMapping
                    maskBrush.SpreadMethod = OpacityMaskBrush.SpreadMethod
                Else
                    If maskBrush.Stops.Count <> 1 Then
                        maskBrush.Stops.Clear()
                        maskBrush.Stops.Add(New Primitives.XamlGradientStop())
                    End If
                    maskBrush.Stops(0).Color = MaskBackgroundColor.Invoke()
                End If
                ' VisualBrush masks are relative to the arranged image, AFTER
                ' UniformToFill cropping, not the original photo's aspect ratio.
                Dim crop = If(source, New Rectangle(0, 0, _texture.Width, _texture.Height))
                Dim mask = maskBrush.GetTexture(Scene.graphicsDevice, destination.Width, destination.Height,
                                               If(MaskCornerRadii Is Nothing,
                                                  New Primitives.CornerRadii(If(MaskCornerRadius Is Nothing, 0.0F, MaskCornerRadius.Invoke())),
                                                  MaskCornerRadii.Invoke()))
                If maskedSource IsNot _texture OrElse maskRevision <> maskBrush.Revision OrElse crop <> previousMaskCrop Then
                    If maskedSource IsNot _texture Then
                        originalPixels = New Color(_texture.Width * _texture.Height - 1) {}
                        maskedPixels = New Color(originalPixels.Length - 1) {}
                    End If
                    If maskedSource IsNot _texture OrElse sourcePixelsDirty Then
                        _texture.GetData(originalPixels)
                        sourcePixelsDirty = False
                    End If
                    maskPixels = maskBrush.PixelData
                    Array.Copy(originalPixels, maskedPixels, originalPixels.Length)
                    Dim maskWidth = mask.Width
                    Dim maskHeight = mask.Height
                    Dim sourceWidth = _texture.Width
                    Threading.Tasks.Parallel.For(0, 4,
                        Sub(band)
                            For y = crop.Top + crop.Height * band \ 4 To crop.Top + crop.Height * (band + 1) \ 4 - 1
                                For x = crop.Left To crop.Right - 1
                                    Dim alpha = maskPixels(((y - crop.Top) * maskHeight \ crop.Height) * maskWidth +
                                                           (x - crop.Left) * maskWidth \ crop.Width).A / 255.0F
                                    maskedPixels(y * sourceWidth + x) *= alpha
                                Next
                            Next
                        End Sub)
                    If maskedTexture Is Nothing OrElse maskedTexture.Width <> _texture.Width OrElse maskedTexture.Height <> _texture.Height Then
                        maskedTexture?.Dispose()
                        maskedTexture = New Texture2D(Scene.graphicsDevice, _texture.Width, _texture.Height)
                    End If
                    maskedTexture.SetData(maskedPixels)
                    maskedSource = _texture
                    maskRevision = maskBrush.Revision
                    previousMaskCrop = crop
                End If
                drawTexture = maskedTexture
            End If
            spriteBatch.Draw(drawTexture, destination, source, ApplyOpacity(Tint))
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                maskedTexture?.Dispose()
                maskBrush?.Dispose()
            End If
            If disposing AndAlso _ownsTexture AndAlso _texture IsNot Nothing Then _texture.Dispose()
            _texture = Nothing
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
