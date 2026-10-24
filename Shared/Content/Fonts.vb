Namespace [Shared].Content
    Partial Public Class Fonts

        Public Class ProjectZSans
            Public Shared ReadOnly Property GetResourceName(FontSize As Integer) As String
                Get
                    Return "ProjectZSans_" & FontSize
                End Get
            End Property
        End Class

        Public Class SegoeUI

            Public Shared ReadOnly Property GetResourceName(FontSize As Integer) As String
                Get
                    Return ProjectZSans.GetResourceName(FontSize)
                End Get
            End Property

        End Class

    End Class

End Namespace
