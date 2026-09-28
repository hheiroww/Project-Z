Imports System.Xml
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.Designer
    Public Partial Class SceneXamlParser
        Private Shared ReadOnly effectAdapters As New Dictionary(Of String, Func(Of XmlElement, XamlNativeEffect))(StringComparer.Ordinal)
        Public ReadOnly Property CompatibilityNotes As New List(Of String)

        ''' <summary>Register explicit native adapters; arbitrary WPF shader bytecode is not executable by DX12.</summary>
        Public Shared Sub RegisterEffectAdapter(xamlType As String, factory As Func(Of XmlElement, XamlNativeEffect))
            effectAdapters(xamlType) = factory
        End Sub

        Private Sub ApplyXamlEffect(target As SceneElement, xml As XmlElement)
            Dim effectXml = TryCast(xml.SelectSingleNode("*[contains(local-name(), '.Effect')]/*"), XmlElement)
            If effectXml Is Nothing Then effectXml = Resource(xml, xml.GetAttribute("Effect"))
            If effectXml Is Nothing Then Return
            Dim factory As Func(Of XmlElement, XamlNativeEffect) = Nothing
            If effectAdapters.TryGetValue(effectXml.LocalName, factory) Then
                target.VisualEffect = factory(effectXml)
                Return
            End If
            Dim effect As XamlNativeEffect
            Select Case effectXml.LocalName
                Case "BlurEffect"
                    effect = New XamlNativeEffect With {.Kind = XamlEffectKind.Blur, .Radius = GetAttributeSingle(effectXml, "Radius", 5)}
                    If effectXml.GetAttribute("KernelType") = "Box" Then CompatibilityNotes.Add("BlurEffect Box kernel uses the native Gaussian approximation.")
                Case "DropShadowEffect"
                    effect = New XamlNativeEffect With {.Kind = XamlEffectKind.DropShadow,
                        .Radius = GetAttributeSingle(effectXml, "BlurRadius", 5), .ShadowDepth = GetAttributeSingle(effectXml, "ShadowDepth", 5),
                        .Direction = GetAttributeSingle(effectXml, "Direction", 315), .Opacity = GetAttributeSingle(effectXml, "Opacity", 1),
                        .Color = GetAttributeColor(effectXml, "Color", Color.Black)}
                Case "SpasticChromaticAbberationEffect", "SpasticChromaticAberrationEffect"
                    effect = New XamlNativeEffect With {.Kind = XamlEffectKind.SpasticChromaticAberration, .Amount = GetAttributeSingle(effectXml, "Amount", 0), .Phase = GetAttributeSingle(effectXml, "Phase", 0), .Radius = 0}
                Case "InvertEffect"
                    effect = New XamlNativeEffect With {.Kind = XamlEffectKind.Invert, .Amount = GetAttributeSingle(effectXml, "Amount", 0), .Radius = 0}
                Case "LinearChromaticAbberationEffect", "LinearChromaticAberrationEffect"
                    effect = New XamlNativeEffect With {.Kind = XamlEffectKind.LinearChromaticAberration, .Amount = GetAttributeSingle(effectXml, "Amount", 0), .Angle = GetAttributeSingle(effectXml, "Angle", 0), .Radius = 0}
                Case "ChromaticAberrationEffect", "ChromaticAbberationEffect"
                    effect = New XamlNativeEffect With {.Kind = XamlEffectKind.ChromaticAberration, .Amount = GetAttributeSingle(effectXml, "Amount", 0), .Radius = 0}
                    CompatibilityNotes.Add(effectXml.LocalName & " uses the Project-Z RGB-offset shader approximation.")
                Case Else
                    _unsupportedFeatures.Add("Shader effect " & effectXml.LocalName & " requires a native adapter (RegisterEffectAdapter); WPF pixel bytecode is not reused.")
                    Return
            End Select
            target.VisualEffect = effect
        End Sub
    End Class
End Namespace
