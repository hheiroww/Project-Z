Namespace [Shared].Drawing

    Public Enum ProjectZGraphicsBackend
        DirectX12
        DirectX11
    End Enum

    Public NotInheritable Class GraphicsBackendInfo

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property Current As ProjectZGraphicsBackend
            Get
#If PROJECTZ_DIRECTX11 Then
                Return ProjectZGraphicsBackend.DirectX11
#Else
                Return ProjectZGraphicsBackend.DirectX12
#End If
            End Get
        End Property

        Public Shared ReadOnly Property SupportsDirectX12 As Boolean
            Get
                Return Current = ProjectZGraphicsBackend.DirectX12
            End Get
        End Property

    End Class

End Namespace
