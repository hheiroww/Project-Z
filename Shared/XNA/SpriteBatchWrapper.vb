Imports System.Text
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].XNA

    Public Class SpriteBatchWrapper
        Implements IDisposable

        Public Property SpriteBatch As SpriteBatch
            Get
                If _SpriteBatch Is Nothing AndAlso _graphicsDevice IsNot Nothing Then
                    _SpriteBatch = New SpriteBatch(_graphicsDevice)
                    _ownsSpriteBatch = True
                End If
                Return _SpriteBatch
            End Get
            Set(value As SpriteBatch)
                _SpriteBatch = value
            End Set
        End Property

        Private _SpriteBatch As SpriteBatch
        Private _graphicsDevice As GraphicsDevice
        Private _ownsSpriteBatch As Boolean = False

        Public ReadOnly Property GraphicsDevice As GraphicsDevice
            Get
                Return SpriteBatch.GraphicsDevice
            End Get
        End Property

        Public Property Settings As SpriteBatchPropertySet
            Get
                Return _Settings
            End Get
            Set(value As SpriteBatchPropertySet)
                _Settings = value
                _HasSettings = _Settings IsNot Nothing
            End Set
        End Property
        Private _Settings As SpriteBatchPropertySet
        Private _HasSettings As Boolean = False

        Public Property isRendering As Boolean = False
        Friend Property RenderOffset As Vector2

        Public Overloads Sub Begin()
            If isRendering Then Return
            If _HasSettings Then
                Settings.Begin(SpriteBatch)
            Else
                ' Linear sampling is the correct UI default. Point sampling makes
                ' scaled images, glyphs and imported XAML assets visibly jagged.
                SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                                  SamplerState.LinearClamp, DepthStencilState.None,
                                  RasterizerState.CullNone, Nothing, Matrix.CreateTranslation(-RenderOffset.X, -RenderOffset.Y, 0))
            End If
            isRendering = True
        End Sub

        Public Overloads Sub Begin(Settings As SpriteBatchPropertySet)
            If isRendering Then Return
            Settings.Begin(SpriteBatch)
            isRendering = True
        End Sub

        Public Overloads Sub Begin(SpriteSortMode As SpriteSortMode, BlendState As BlendState)
            SpriteBatch.Begin(SpriteSortMode, BlendState)
            isRendering = True
        End Sub

        Public Overloads Sub Begin(SpriteSortMode As SpriteSortMode, BlendState As BlendState, SamplerState As SamplerState, DepthStencilState As DepthStencilState, RasterizerState As RasterizerState)
            SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Nothing, Matrix.CreateTranslation(-RenderOffset.X, -RenderOffset.Y, 0))
            isRendering = True
        End Sub

        Public Overloads Sub Begin(SpriteSortMode As SpriteSortMode, BlendState As BlendState, SamplerState As SamplerState, DepthStencilState As DepthStencilState, RasterizerState As RasterizerState, Effect As Effect)
            SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, Matrix.CreateTranslation(-RenderOffset.X, -RenderOffset.Y, 0))
            isRendering = True
        End Sub

        Public Sub Draw(texture As Texture2D, rectangle As Rectangle, color As Color)
            Try
                SpriteBatch.Draw(texture, rectangle, color)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub Draw(texture As Texture2D, position As Vector2, color As Color)
            Try
                SpriteBatch.Draw(texture, position, color)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub Draw(texture As Texture2D, destinationRectangle As Rectangle, sourceRectangle As Rectangle?, color As Color)
            Try
                SpriteBatch.Draw(texture, destinationRectangle, sourceRectangle, color)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub Draw(texture As Texture2D, position As Vector2, sourceRectangle As Rectangle?, color As Color)
            Try
                SpriteBatch.Draw(texture, position, sourceRectangle, color)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub Draw(texture As Texture2D, destinationRectangle As Rectangle, sourceRectangle As Rectangle?, color As Color, rotation As Single, origin As Vector2, effect As SpriteEffects, depth As Single)
            Try
                SpriteBatch.Draw(texture, destinationRectangle, sourceRectangle, color, rotation, origin, effect, depth)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub Draw(texture As Texture2D, position As Vector2, sourceRectangle As Rectangle?, color As Color, rotation As Single, origin As Vector2, scale As Single, effect As SpriteEffects, depth As Single)
            Try
                SpriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effect, depth)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub Draw(texture As Texture2D, position As Vector2, sourceRectangle As Rectangle?, color As Color, rotation As Single, origin As Vector2, scale As Vector2, effect As SpriteEffects, depth As Single)
            Try
                SpriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effect, depth)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub DrawString(spriteFont As SpriteFont, text As StringBuilder, position As Vector2, color As Color)
            Try
                SpriteBatch.DrawString(spriteFont, text, position, color)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub DrawString(spriteFont As SpriteFont, text As String, position As Vector2, color As Color)
            Try
                SpriteBatch.DrawString(spriteFont, text, position, color)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub DrawString(spriteFont As SpriteFont, text As StringBuilder, position As Vector2, color As Color, rotation As Single, origin As Vector2, scale As Single, effects As SpriteEffects, depth As Single)
            Try
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effects, depth)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub DrawString(spriteFont As SpriteFont, text As StringBuilder, position As Vector2, color As Color, rotation As Single, origin As Vector2, scale As Vector2, effect As SpriteEffects, depth As Single)
            Try
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effect, depth)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub DrawString(spriteFont As SpriteFont, text As String, position As Vector2, color As Color, rotation As Single, origin As Vector2, scale As Vector2, effect As SpriteEffects, depth As Single)
            Try
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effect, depth)
            Catch ex As Exception

            End Try
        End Sub
        Public Sub DrawString(spriteFont As SpriteFont, text As String, position As Vector2, color As Color, rotation As Single, origin As Vector2, scale As Single, effects As SpriteEffects, depth As Single)
            Try
                SpriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effects, depth)
            Catch ex As Exception

            End Try
        End Sub

        Public Sub [End]()
            If Not isRendering Then Return
            SpriteBatch.[End]()
            isRendering = False
        End Sub

        Public Sub New(SpriteBatch As SpriteBatch)
            Me.SpriteBatch = SpriteBatch
            _graphicsDevice = SpriteBatch?.GraphicsDevice
            _ownsSpriteBatch = False
        End Sub

        Public Sub New(graphicsDevice As GraphicsDevice)
            _graphicsDevice = graphicsDevice
        End Sub

        Public Sub New(graphicsDevice As GraphicsDevice, Settings As SpriteBatchPropertySet)
            _graphicsDevice = graphicsDevice
            Me.Settings = Settings
        End Sub

#Region "IDisposable Support"
        Private disposedValue As Boolean

        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then
                    ' Only dispose the SpriteBatch if we created it
                    If _ownsSpriteBatch AndAlso _SpriteBatch IsNot Nothing Then
                        _SpriteBatch.Dispose()
                    End If
                    _SpriteBatch = Nothing
                End If
                disposedValue = True
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region
    End Class

End Namespace
