Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].XNA

    Public Class SpriteBatchPropertySet

        Public Property SpriteSortMode As SpriteSortMode
        Public Property BlendState As BlendState
        Public Property SamplerState As SamplerState
        Public Property DepthStencilState As DepthStencilState
        Public Property RasterizerState As RasterizerState
        Public Property Effect As Effect
            Get
                Return _Effect
            End Get
            Set(value As Effect)
                _Effect = value
                If value IsNot Nothing Then
                    _UseEffect = True
                Else
                    _UseEffect = False
                End If
            End Set
        End Property
        Private _Effect As Effect

        Public ReadOnly Property UseEffect As Boolean
            Get
                Return _UseEffect
            End Get
        End Property
        Private _UseEffect As Boolean = False

        Public Sub Begin(SpriteBatch As SpriteBatch)
            Dim scale = Drawing.GraphicsQuality.ForDevice(SpriteBatch.GraphicsDevice).RenderScale
            Dim transform = Microsoft.Xna.Framework.Matrix.CreateScale(scale.X, scale.Y, 1)
            If UseEffect Then
                SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, transform)
            Else
                SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Nothing, transform)
            End If
        End Sub

        Public Sub New(SpriteSortMode As SpriteSortMode, BlendState As BlendState)
            Me.SpriteSortMode = SpriteSortMode
            Me.BlendState = BlendState
        End Sub

        Public Sub New(SpriteSortMode As SpriteSortMode, BlendState As BlendState, SamplerState As SamplerState, DepthStencilState As DepthStencilState, RasterizerState As RasterizerState)
            Me.SpriteSortMode = SpriteSortMode
            Me.BlendState = BlendState
            Me.SamplerState = SamplerState
            Me.DepthStencilState = DepthStencilState
            Me.RasterizerState = RasterizerState
        End Sub

        Public Sub New(SpriteSortMode As SpriteSortMode, BlendState As BlendState, SamplerState As SamplerState, DepthStencilState As DepthStencilState, RasterizerState As RasterizerState, Effect As Effect)
            Me.SpriteSortMode = SpriteSortMode
            Me.BlendState = BlendState
            Me.SamplerState = SamplerState
            Me.DepthStencilState = DepthStencilState
            Me.RasterizerState = RasterizerState
            Me.Effect = Effect
        End Sub
    End Class

End Namespace
