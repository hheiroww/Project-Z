Imports System.IO
Imports System.Xml
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.Designer
    Public Partial Class SceneXamlParser
        Private Shared ReadOnly applicationDictionaries As New List(Of XmlElement)
        Private Shared ReadOnly dictionaryCache As New Dictionary(Of String, XmlElement)(StringComparer.OrdinalIgnoreCase)
        Private importedDictionary As XmlElement
        Public WriteOnly Property ImportedResourceMarkup As String
            Set(value As String)
                Dim document As New XmlDocument With {.XmlResolver = Nothing}
                document.LoadXml(value)
                importedDictionary = document.DocumentElement
            End Set
        End Property
        Public Shared Sub UpdateRegisteredStyle(path As String, key As String, [property] As String, value As String)
            Dim dictionary = LoadDictionary(path)
            For Each style In dictionary.GetElementsByTagName("Style").OfType(Of XmlElement)()
                If style.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml") <> key Then Continue For
                Dim setter = dictionary.OwnerDocument.CreateElement("Setter", style.NamespaceURI)
                setter.SetAttribute("Property", [property]) : setter.SetAttribute("Value", value)
                style.AppendChild(setter)
            Next
        End Sub

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
            If importedDictionary IsNot Nothing Then
                For Each entry In DictionaryEntries(importedDictionary, visited) : Yield entry : Next
            End If
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
            Return Resources(scope).FirstOrDefault(Function(entry) entry.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml") = key)
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

        Public Function ReadImportedGradient(xaml As String) As XamlGradientBrush
            Dim document As New XmlDocument With {.XmlResolver = Nothing}
            document.LoadXml(xaml)
            Dim brush = document.DocumentElement
            Dim owner = document.CreateElement("Border", brush.NamespaceURI)
            Dim propertyNode = document.CreateElement("Border.Background", brush.NamespaceURI)
            document.RemoveChild(brush)
            document.AppendChild(owner)
            owner.AppendChild(propertyNode)
            propertyNode.AppendChild(brush)
            Return ReadGradientBrush(owner, "Background")
        End Function

        Public Function ReadImportedColor(value As String) As Color
            Return ParseColor(value, Color.Transparent)
        End Function

        Private Sub ApplyResolvedStyle(element As XmlElement)
            CopyTemplateSurface(element, TryCast(element.SelectSingleNode("*[contains(local-name(), '.Template')]/*[local-name()='ControlTemplate']"), XmlElement))
            Dim style = Resource(element, element.GetAttribute("Style"))
            If style Is Nothing AndAlso Not element.HasAttribute("Style") Then
                style = Resources(element).FirstOrDefault(Function(entry) entry.LocalName = "Style" AndAlso entry.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml") = "" AndAlso
                    entry.GetAttribute("TargetType").Replace("{x:Type ", "").TrimEnd("}"c) = element.LocalName)
            End If
            Dim visited As New HashSet(Of XmlElement)
            While style IsNot Nothing AndAlso visited.Add(style)
                For Each setter As XmlElement In style.SelectNodes("*[local-name()='Setter'][@Property!='Template']")
                    Dim name = setter.GetAttribute("Property")
                    ' Window theme entry animations are not the control's final
                    ' opacity. Do not make a native host invisible while those
                    ' unnamed application-level storyboards are unsupported.
                    If name = "Opacity" AndAlso element.LocalName = "Window" AndAlso
                       style.SelectSingleNode(".//*[local-name()='EventTrigger'][@RoutedEvent='Loaded']") IsNot Nothing Then Continue For
                    If element.HasAttribute(name) OrElse element.SelectSingleNode("*[substring-after(local-name(), '.')='" & name & "']") IsNot Nothing Then Continue For
                    If setter.HasAttribute("Value") OrElse name = "CornerRadius" Then
                        element.SetAttribute(name, If(setter.HasAttribute("Value"), setter.GetAttribute("Value"), setter.InnerText.Trim()))
                    Else
                        Dim value = TryCast(setter.SelectSingleNode("*[local-name()='Setter.Value']/*"), XmlElement)
                        If value IsNot Nothing Then
                            Dim propertyNode = element.OwnerDocument.CreateElement(element.Prefix, element.LocalName & "." & name, element.NamespaceURI)
                            propertyNode.AppendChild(element.OwnerDocument.ImportNode(value, True))
                            element.AppendChild(propertyNode)
                        End If
                    End If
                Next
                CopyTemplateSurface(element, TryCast(style.SelectSingleNode("*[local-name()='Setter'][@Property='Template']/*[local-name()='Setter.Value']/*[local-name()='ControlTemplate']"), XmlElement))
                For Each trigger As XmlElement In style.SelectNodes("*[local-name()='Style.Triggers']/*[local-name()='DataTrigger' or local-name()='EventTrigger' or local-name()='Trigger']")
                    If trigger.LocalName = "Trigger" AndAlso Not ImportedEventRouting Then Continue For
                    Dim propertyName = element.LocalName & ".Triggers"
                    Dim triggers = TryCast(element.SelectSingleNode("*[local-name()='" & propertyName & "']"), XmlElement)
                    If triggers Is Nothing Then
                        triggers = element.OwnerDocument.CreateElement(propertyName, element.NamespaceURI)
                        element.AppendChild(triggers)
                    End If
                    If Not triggers.ChildNodes.OfType(Of XmlElement)().Any(Function(n) n.OuterXml = trigger.OuterXml) Then triggers.AppendChild(element.OwnerDocument.ImportNode(trigger, True))
                Next
                For Each state In {("IsMouseOver", "MouseOverBackground"), ("IsPressed", "MouseDownBackground")}
                    ' Imported adapters already route these Trigger setters through
                    ' native events; synthetic Button aliases are unnecessary there.
                    If ImportedEventRouting Then Continue For
                    Dim setter = TryCast(style.SelectSingleNode(".//*[local-name()='Trigger'][@Property='" & state.Item1 & "'][@Value='True']/*[local-name()='Setter'][@Property='Background']"), XmlElement)
                    If setter IsNot Nothing AndAlso Not element.HasAttribute(state.Item2) Then element.SetAttribute(state.Item2, setter.GetAttribute("Value"))
                Next
                style = Resource(style, style.GetAttribute("BasedOn"))
            End While
            CopyTemplateSurface(element, Resource(element, element.GetAttribute("Template")))
        End Sub

        ''' <summary>Expand the same native style chain before codegen binds CLR adapter properties.</summary>
        Public Function ExpandImportedStyles(xaml As String) As String
            Dim document As New XmlDocument With {.XmlResolver = Nothing}
            document.LoadXml(xaml)
            For Each element As XmlElement In document.SelectNodes("//*").Cast(Of XmlElement)().ToArray()
                If Not _elementFactories.ContainsKey(element.LocalName) Then Continue For
                Dim ancestor = element.ParentNode
                Dim declaration = False
                While ancestor IsNot Nothing
                    If ancestor.LocalName = "Style" OrElse ancestor.LocalName.EndsWith("Template", StringComparison.Ordinal) OrElse ancestor.LocalName = "ResourceDictionary" OrElse ancestor.LocalName.EndsWith(".Resources", StringComparison.Ordinal) Then
                        declaration = True
                        Exit While
                    End If
                    ancestor = ancestor.ParentNode
                End While
                If Not declaration Then ApplyResolvedStyle(element)
            Next
            Return document.OuterXml
        End Function

        ''' <summary>Apply linked XAML appearance to a native code-behind control without replacing its handlers or layout.</summary>
        Public Sub ApplyAppearance(target As RectangleElement, filePath As String, Optional name As String = Nothing, Optional controlType As String = Nothing)
            Dim doc As New XmlDocument With {.XmlResolver = Nothing}
            doc.Load(filePath)
            Dim xml = If(name Is Nothing, Nothing, doc.SelectNodes("//*").Cast(Of XmlElement)().FirstOrDefault(Function(n) n.GetAttribute("Name", "http://schemas.microsoft.com/winfx/2006/xaml") = name))
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
