Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.XNA

Namespace [Shared].Drawing.UI.Advanced

    Public Class SceneProjectionHost
        Inherits SceneElement

        Public Property Scale As Double
            Get
                Return _Scale
            End Get
            Set(value As Double)
                _Scale = Math.Max(0.01, Math.Min(1, value))
                CheckInterp()
                CanSetSize = True
            End Set
        End Property
        Private _Scale As Double = 1

        Public Property RenderOptions As SpriteBatchPropertySet

        Private AllowInterp As Boolean = False
        Private CanSetSize As Boolean = True

        Public Property SimulatedBounds As Rectangle
            Get
                Return _SimulatedBounds
            End Get
            Set(value As Rectangle)
                _SimulatedBounds = value
                CheckInterp()
            End Set
        End Property
        Private _SimulatedBounds As New Rectangle(0, 0, 0, 0)

        Public ReadOnly Property Texture As Texture2D
            Get
                Return _Texture
            End Get
        End Property
        Private _Texture As Texture2D

        Public Property TargetScene As Scene
            Get
                Return _TargetScene
            End Get
            Set(value As Scene)
                _TargetScene = value
                If value IsNot Nothing Then value.UseDesktopBackdrop = False
            End Set
        End Property
        Private _TargetScene As Scene

#Region "Constructors"

        Public Sub New(Scene As Scene, TargetScene As Scene)
            MyBase.New(Scene, Scene.GetSpriteBatch)

            RenderOptions = New SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                                                                   SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone)
            SimulatedBounds = New Rectangle(0, 0,
                                            TargetScene.graphicsDevice.PresentationParameters.BackBufferWidth,
                                            TargetScene.graphicsDevice.PresentationParameters.BackBufferHeight)
            Me.Size = New Vector2(CInt(SimulatedBounds.Width), CInt(SimulatedBounds.Height))
            TargetScene.RenderTargetOptions = RenderOptions
            TargetScene.UseRenderTarget = True
            spriteBatch.Settings = RenderOptions
            Me.TargetScene = TargetScene
            _Texture = TargetScene.Texture
            Scene.UseRenderTarget = True
            Scene.AddProjectionHost(Me)
        End Sub

#End Region

        Public Overloads Function Interp(p As Point) As Point
            If AllowInterp Then
                Interp.X = CInt(DoubleAnimation.Interpolate((p.X - Position.X), 0, Rectangle.Width, 0, SimulatedBounds.Width))
                Interp.Y = CInt(DoubleAnimation.Interpolate((p.Y - Position.Y), 0, Rectangle.Height, 0, SimulatedBounds.Height))
            Else
                Interp.X = 0
                Interp.Y = 0
            End If
        End Function

        Public Overloads Function Interp(mouseState As MouseState) As MouseState
            Dim InterpolatedPoint As Point = Interp(mouseState.Position)
            Return New MouseState(InterpolatedPoint.X, InterpolatedPoint.Y,
                                  mouseState.ScrollWheelValue, mouseState.LeftButton,
                                  mouseState.MiddleButton, mouseState.RightButton,
                                  mouseState.XButton1, mouseState.XButton2)
        End Function

        Protected Friend Overrides Sub doDraw(gameTime As GameTime)
            TargetScene.Tick(gameTime)

            Dim RT As RenderTarget2D = TargetScene.DrawToRenderTarget
            Dim sR As New Rectangle(CInt(SimulatedBounds.X * Scale), CInt(SimulatedBounds.Y * Scale), SimulatedBounds.Width, SimulatedBounds.Height)
            spriteBatch.Begin(RenderOptions)
            spriteBatch.Draw(RT, Rectangle, sR, Color.White)
            spriteBatch.End()
            ChangeRenderSize()
        End Sub

        Protected Friend Overrides Function ContainsPoint(p As Point) As Boolean
            Return TargetScene.PointToElement(p) IsNot Nothing
        End Function

        Protected Friend Overrides Sub Draw(gameTime As GameTime)

        End Sub

        Private Sub SceneProjection_OnSizeChange(oldSize As Vector2, newSize As Vector2) Handles Me.SizeChanged
            CheckInterp()
            CanSetSize = True
        End Sub

        Private Sub ChangeRenderSize()
            If CanSetSize Then
                CanSetSize = False
                TargetScene.renderTarget.Dispose()
                TargetScene.renderTarget = New RenderTarget2D(TargetScene.graphicsDevice, CInt(SimulatedBounds.Width * Scale), CInt(SimulatedBounds.Height * Scale),
                                  False, TargetScene.graphicsDevice.PresentationParameters.BackBufferFormat,
                                  DepthFormat.Depth24Stencil8)

            End If
        End Sub

        Private Sub CheckInterp()
            AllowInterp = (Size.X > 0 And Size.Y > 0) And (SimulatedBounds.Width > 0 And SimulatedBounds.Height > 0) And (Rectangle.Height > 0 And Rectangle.Width > 0)
        End Sub

        Protected Overrides Sub Finalize()
            MyBase.Finalize()
            Scene.RemoveProjectionHost(Me)
        End Sub
    End Class

End Namespace
