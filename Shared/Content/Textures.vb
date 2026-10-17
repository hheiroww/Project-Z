Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports System.Runtime.CompilerServices

Namespace [Shared].Content

    Public Class Textures

        Private Shared ReadOnly whiteTextures As New ConditionalWeakTable(Of GraphicsDevice, SharedWhiteTexture)
        Private NotInheritable Class SharedWhiteTexture
            Public ReadOnly Texture As Texture2D
            Public Sub New(device As GraphicsDevice)
                Texture = CreateSolidTexture(device, Color.White)
                AddHandler device.Disposing, AddressOf DeviceDisposing
            End Sub
            Private Sub DeviceDisposing(sender As Object, args As EventArgs)
                Texture.Dispose()
                RemoveHandler DirectCast(sender, GraphicsDevice).Disposing, AddressOf DeviceDisposing
                whiteTextures.Remove(DirectCast(sender, GraphicsDevice))
            End Sub
        End Class

        ' Borrowed by rectangle primitives; lifetime belongs to their graphics device.
        Friend Shared Function GetSharedWhiteTexture(device As GraphicsDevice) As Texture2D
            Return whiteTextures.GetValue(device, Function(owner) New SharedWhiteTexture(owner)).Texture
        End Function

        Public Property GraphicsDevice As GraphicsDevice

        Public Overloads Shared Function CreateSolidTexture(GraphicsDevice As GraphicsDevice, Color As Color) As Texture2D
            Dim t As New Texture2D(GraphicsDevice, 1, 1)
            t.SetData(Of Color)({Color})
            Return t
        End Function

        Public Sub New(GraphicsDevice As GraphicsDevice)
            Me.GraphicsDevice = GraphicsDevice
        End Sub
    End Class

End Namespace
