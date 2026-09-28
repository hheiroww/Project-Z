Imports Microsoft.Xna.Framework

Namespace [Shared].Drawing.UI.Input
    ''' <summary>
    ''' Project-Z equivalent of a password input. It deliberately reuses the
    ''' framework textbox so focus, selection, keyboard repeat, and layout stay
    ''' identical while the glyphs are masked and clipboard export is disabled.
    ''' </summary>
    <Serializable>
    Public Class PasswordBox
        Inherits Textbox

        Public Property Password As String
            Get
                Return Text
            End Get
            Set(value As String)
                Text = If(value, String.Empty)
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            MaskCharacter = ChrW(&H2022)
            AcceptsReturn = False
        End Sub
    End Class
End Namespace
