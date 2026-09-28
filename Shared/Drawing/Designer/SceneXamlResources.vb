Imports System.IO
Imports System.Xml
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.Designer
    Public Partial Class SceneXamlParser
        Private Shared ReadOnly applicationDictionaries As New List(Of XmlElement)
        Private Shared ReadOnly dictionaryCache As New Dictionary(Of String, XmlElement)(StringComparer.OrdinalIgnoreCase)

        ''' <summary>Register the application's ORIGINAL linked resource dictionary before importing windows.</summary>
        Public Shared Sub RegisterApplicationResources(path As String)
            Dim dictionary = LoadDictionary(path)
            SyncLock applicationDictionaries
                If Not applicationDictionaries.Contains(dictionary) Then applicationDictionaries.Add(dictionary)
            End SyncLock
        End Sub

        Private Shared Function LoadDictionary(path As String) As XmlElement
            path = IO.Path.GetFullPath(path)
            SyncLock dictionaryCache
                Dim result As XmlElement = Nothing
                If Not dictionaryCache.TryGetValue(path, result) Then
                    Dim document As New XmlDocument With {.XmlResolver = Nothing}
                    document.Load(path)
                    result = document.DocumentElement
                    If result.LocalName = "Application" Then result = DirectCast(result.SelectSingleNode("*[local-name()='Application.Resources']"), XmlElement)
                    dictionaryCache(path) = result
                End If
                Return result
            End SyncLock
        End Function

        Private Iterator Function DictionaryEntries(dictionary As XmlElement, visited As HashSet(Of XmlElement)) As IEnumerable(Of XmlElement)
            If Not visited.Add(dictionary) Then Return
            For Each child As XmlElement In dictionary.ChildNodes.OfType(Of XmlElement)()
                If child.LocalName = "ResourceDictionary" Then
                    For Each entry In DictionaryEntries(child, visited) : Yield entry : Next
                ElseIf Not child.LocalName.Contains(".") Then
                    Yield child
                End If
            Next
            For Each merged As XmlElement In dictionary.SelectNodes("*[local-name()='ResourceDictionary.MergedDictionaries']/*").Cast(Of XmlElement)().Reverse()
                Dim source = merged.GetAttribute("Source")
                Dim resolved = merged
                If source.Length > 0 Then
                    Dim baseUri As Uri = Nothing
                    If Uri.TryCreate(merged.BaseURI, UriKind.Absolute, baseUri) AndAlso baseUri.IsFile Then
                        Dim path = IO.Path.GetFullPath(IO.Path.Combine(IO.Path.GetDirectoryName(baseUri.LocalPath), source))
                        If IO.File.Exists(path) Then
                            resolved = LoadDictionary(path)
                        Else
                            SyncLock dictionaryCache
                                Dim registered = dictionaryCache.FirstOrDefault(Function(entry) IO.Path.GetFileName(entry.Key).Equals(IO.Path.GetFileName(source), StringComparison.OrdinalIgnoreCase))
                                If registered.Value IsNot Nothing Then resolved = registered.Value
                            End SyncLock
                        End If
                    ElseIf IO.Path.IsPathRooted(source) AndAlso IO.File.Exists(source) Then
                        resolved = LoadDictionary(source)
                    Else
                        _unsupportedFeatures.Add("ResourceDictionary.Source requires a linked local file: " & source)
                    End If
                End If
                For Each entry In DictionaryEntries(resolved, visited) : Yield entry : Next
            Next
        End Function

        Private Iterator Function Resources(scope As XmlElement) As IEnumerable(Of XmlElement)
            Dim visited As New HashSet(Of XmlElement)
            Dim node As XmlNode = scope
            While node IsNot Nothing
                If TypeOf node Is XmlElement AndAlso node.LocalName = "ResourceDictionary" Then
                    For Each entry In DictionaryEntries(DirectCast(node, XmlElement), visited) : Yield entry : Next
                End If
                For Each dictionary As XmlElement In node.SelectNodes("*[contains(local-name(), '.Resources')]")
                    For Each entry In DictionaryEntries(dictionary, visited) : Yield entry : Next
                Next
                node = node.ParentNode
            End While
            Dim dictionaries As XmlElement()
            SyncLock applicationDictionaries
                dictionaries = applicationDictionaries.ToArray()
            End SyncLock
            For Each dictionary In dictionaries.Reverse()
                For Each entry In DictionaryEntries(dictionary, visited) : Yield entry : Next
            Next
        End Function

        Private Function Resource(scope As XmlElement, expression As String) As XmlElement
            If Not (expression.StartsWith("{StaticResource ") OrElse expression.StartsWith("{DynamicResource ")) Then Return Nothing
            Dim key = expression.Substring(expression.IndexOf(" "c) + 1).TrimEnd("}"c).Trim()
            Return Resources(scope).FirstOrDefault(Function(entry) entry.GetAttribute("x:Key") = key)
        End Function

        Private Function ResolveColorText(scope As XmlElement, value As String, Optional depth As Integer = 0) As String
            If depth > 12 Then Return "Transparent"
            Dim entry = Resource(scope, value)
            If entry Is Nothing Then Return value
            If entry.LocalName = "SolidColorBrush" Then Return ResolveColorText(entry, entry.GetAttribute("Color"), depth + 1)
            If entry.LocalName = "Color" Then Return entry.InnerText.Trim()
            Return value
        End Function

        Private Function ReadGradientBrush(scope As XmlElement, propertyName As String) As XamlGradientBrush
            Dim brushXml = TryCast(scope.SelectSingleNode("*[substring-after(local-name(), '.')='" & propertyName & "']/*"), XmlElement)
            If brushXml Is Nothing Then brushXml = Resource(scope, scope.GetAttribute(propertyName))
            If brushXml Is Nothing OrElse Not {"LinearGradientBrush", "RadialGradientBrush"}.Contains(brushXml.LocalName) Then Return Nothing
            Dim brush As New XamlGradientBrush With {
                .IsRadial = brushXml.LocalName = "RadialGradientBrush",
                .StartPoint = ParseGradientPoint(brushXml.GetAttribute("StartPoint"), Vector2.Zero),
                .EndPoint = ParseGradientPoint(brushXml.GetAttribute("EndPoint"), Vector2.One),
                .Center = ParseGradientPoint(brushXml.GetAttribute("Center"), New Vector2(0.5F)),
                .GradientOrigin = ParseGradientPoint(brushXml.GetAttribute("GradientOrigin"), New Vector2(0.5F)),
                .Radius = New Vector2(GetAttributeSingle(brushXml, "RadiusX", 0.5F), GetAttributeSingle(brushXml, "RadiusY", 0.5F)),
                .Opacity = GetAttributeSingle(brushXml, "Opacity", 1),
                .SpreadMethod = GetAttributeString(brushXml, "SpreadMethod", "Pad"),
                .AbsoluteMapping = brushXml.GetAttribute("MappingMode") = "Absolute"}
            For Each stopXml As XmlElement In brushXml.SelectNodes("*[local-name()='GradientStop'] | *[local-name()='GradientBrush.GradientStops' or local-name()='LinearGradientBrush.GradientStops' or local-name()='RadialGradientBrush.GradientStops']/*[local-name()='GradientStop']")
                brush.Stops.Add(New XamlGradientStop With {.Offset = GetAttributeSingle(stopXml, "Offset", 0), .Color = GetAttributeColor(stopXml, "Color", Color.Transparent)})
            Next
            Return brush
        End Function

        Private Sub ApplyResolvedStyle(element As XmlElement)
            Dim style = Resource(element, element.GetAttribute("Style"))
            If style Is Nothing AndAlso Not element.HasAttribute("Style") Then
                style = Resources(element).FirstOrDefault(Function(entry) entry.LocalName = "Style" AndAlso entry.GetAttribute("x:Key") = "" AndAlso
                    entry.GetAttribute("TargetType").Replace("{x:Type ", "").TrimEnd("}"c) = element.LocalName)
            End If
            Dim visited As New HashSet(Of XmlElement)
            While style IsNot Nothing AndAlso visited.Add(style)
                For Each setter As XmlElement In style.SelectNodes("*[local-name()='Setter'][@Value or @Property='CornerRadius']")
                    Dim name = setter.GetAttribute("Property")
                    ' Window theme entry animations are not the control's final
                    ' opacity. Do not make a native host invisible while those
                    ' unnamed application-level storyboards are unsupported.
                    If name = "Opacity" AndAlso element.LocalName = "Window" AndAlso
                       style.SelectSingleNode(".//*[local-name()='EventTrigger'][@RoutedEvent='Loaded']") IsNot Nothing Then Continue For
                    If element.HasAttribute(name) OrElse element.SelectSingleNode("*[substring-after(local-name(), '.')='" & name & "']") IsNot Nothing Then Continue For
                    element.SetAttribute(name, If(setter.HasAttribute("Value"), setter.GetAttribute("Value"), setter.InnerText.Trim()))
                Next
                CopyTemplateSurface(element, TryCast(style.SelectSingleNode("*[local-name()='Setter'][@Property='Template']/*[local-name()='Setter.Value']/*[local-name()='ControlTemplate']"), XmlElement))
                For Each state In {("IsMouseOver", "MouseOverBackground"), ("IsPressed", "MouseDownBackground")}
                    Dim setter = TryCast(style.SelectSingleNode(".//*[local-name()='Trigger'][@Property='" & state.Item1 & "'][@Value='True']/*[local-name()='Setter'][@Property='Background']"), XmlElement)
                    If setter IsNot Nothing AndAlso Not element.HasAttribute(state.Item2) Then element.SetAttribute(state.Item2, setter.GetAttribute("Value"))
                Next
                style = Resource(style, style.GetAttribute("BasedOn"))
            End While
            CopyTemplateSurface(element, Resource(element, element.GetAttribute("Template")))
        End Sub

        ''' <summary>Apply linked XAML appearance to a native code-behind control without replacing its handlers or layout.</summary>
        Public Sub ApplyAppearance(target As RectangleElement, filePath As String, Optional name As String = Nothing, Optional controlType As String = Nothing)
            Dim doc As New XmlDocument With {.XmlResolver = Nothing}
            doc.Load(filePath)
            Dim xml = If(name Is Nothing, Nothing, doc.SelectNodes("//*").Cast(Of XmlElement)().FirstOrDefault(Function(n) n.GetAttribute("x:Name") = name))
            If xml Is Nothing Then
                xml = doc.CreateElement(If(controlType, target.GetType().Name), doc.DocumentElement.NamespaceURI)
                doc.DocumentElement.AppendChild(xml)
            End If
            ApplyResourceStyle(xml)
            target.BackgroundColor = GetAttributeColor(xml, "Background", target.BackgroundColor)
            ApplyBackgroundBrush(target, xml)
            ApplyRoundedSurface(target, xml)
            ApplyXamlEffect(target, xml)
            If TypeOf target Is Button Then
                Dim button = DirectCast(target, Button)
                button.ForegroundColor = GetAttributeColor(xml, "Foreground", button.ForegroundColor)
                button.MouseOverBackgroundColor = GetAttributeColor(xml, "MouseOverBackground", button.MouseOverBackgroundColor)
                button.MouseDownBackgroundColor = GetAttributeColor(xml, "MouseDownBackground", button.MouseDownBackgroundColor)
            End If
        End Sub
    End Class
End Namespace
