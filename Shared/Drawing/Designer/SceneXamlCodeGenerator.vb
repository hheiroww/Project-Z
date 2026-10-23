Imports System.IO
Imports System.Text
Imports System.Xml
Imports Microsoft.Xna.Framework

Namespace [Shared].Drawing.Designer

    ''' <summary>
    ''' Generates VB.NET code from XAML scene definitions.
    ''' This creates designer files similar to WPF's code-behind generation.
    ''' </summary>
    Public Class SceneXamlCodeGenerator

#Region "Fields"

        Private ReadOnly _xamlPath As String
        Private ReadOnly _namespace As String
        Private ReadOnly _className As String
        Private ReadOnly _elements As New List(Of ElementInfo)
        Private ReadOnly _indent As String = "    "

#End Region

#Region "Nested Types"

        Private Class ElementInfo
            Public Property Name As String
            Public Property TypeName As String
            Public Property IsWithEvents As Boolean = False
            Public Property Properties As New Dictionary(Of String, String)
        End Class

#End Region

#Region "Constructors"

        ''' <summary>
        ''' Creates a code generator for the specified XAML file.
        ''' </summary>
        ''' <param name="xamlPath">Path to the XAML file</param>
        ''' <param name="namespace">Target namespace for generated code</param>
        ''' <param name="className">Class name (typically the scene name)</param>
        Public Sub New(xamlPath As String, [namespace] As String, className As String)
            _xamlPath = xamlPath
            _namespace = [namespace]
            _className = className
        End Sub

#End Region

#Region "Public Methods"

        ''' <summary>
        ''' Generates the designer code file (.Designer.vb) content.
        ''' </summary>
        Public Function GenerateDesignerCode() As String
            Dim xaml As String = File.ReadAllText(_xamlPath)
            Dim doc As New XmlDocument()
            doc.LoadXml(xaml)

            ' Parse all elements with x:Name
            ParseElementsRecursive(doc.DocumentElement)

            ' Generate the code
            Return GenerateCode()
        End Function

        ''' <summary>
        ''' Generates and writes the designer file.
        ''' </summary>
        Public Sub GenerateDesignerFile(outputPath As String)
            Dim code As String = GenerateDesignerCode()
            File.WriteAllText(outputPath, code)
        End Sub

        ''' <summary>
        ''' Generates initialization code that loads XAML at runtime.
        ''' </summary>
        Public Function GenerateInitializerCode() As String
            Dim sb As New StringBuilder()

            sb.AppendLine($"{_indent}{_indent}''' <summary>")
            sb.AppendLine($"{_indent}{_indent}''' Initializes UI elements from XAML definition.")
            sb.AppendLine($"{_indent}{_indent}''' This is called automatically by the designer-generated code.")
            sb.AppendLine($"{_indent}{_indent}''' </summary>")
            sb.AppendLine($"{_indent}{_indent}Private Sub InitializeComponent()")
            sb.AppendLine($"{_indent}{_indent}{_indent}Dim parser As New SceneXamlParser(Me)")
            sb.AppendLine($"{_indent}{_indent}{_indent}Dim xamlContent As String = GetXamlContent()")
            sb.AppendLine($"{_indent}{_indent}{_indent}Dim root As SceneElement = parser.Parse(xamlContent)")
            sb.AppendLine()
            sb.AppendLine($"{_indent}{_indent}{_indent}' Add root element and all top-level children to the scene")
            sb.AppendLine($"{_indent}{_indent}{_indent}For Each child As SceneElement In root.Children")
            sb.AppendLine($"{_indent}{_indent}{_indent}{_indent}AddElement(child)")
            sb.AppendLine($"{_indent}{_indent}{_indent}Next")
            sb.AppendLine()
            sb.AppendLine($"{_indent}{_indent}{_indent}' Bind named elements to fields")

            For Each element In _elements
                sb.AppendLine($"{_indent}{_indent}{_indent}{element.Name} = parser.FindName(Of {element.TypeName})(""{element.Name}"")")
            Next

            sb.AppendLine($"{_indent}{_indent}End Sub")

            Return sb.ToString()
        End Function

#End Region

#Region "Private Methods"

        Private Sub ParseElementsRecursive(xmlElement As XmlElement)
            ' Check for x:Name or Name attribute
            Dim name = xmlElement.GetAttribute("Name", "http://schemas.microsoft.com/winfx/2006/xaml")
            If String.IsNullOrEmpty(name) Then
                name = xmlElement.GetAttribute("Name")
            End If

            If Not String.IsNullOrEmpty(name) Then
                Dim elementInfo As New ElementInfo With {
                    .Name = name,
                    .TypeName = MapElementType(xmlElement.LocalName),
                    .IsWithEvents = HasEvents(xmlElement)
                }

                ' Collect properties
                For Each attr As XmlAttribute In xmlElement.Attributes
                    If Not attr.Name.StartsWith("xmlns") AndAlso
                       attr.Name <> "x:Name" AndAlso
                       attr.Name <> "Name" Then
                        elementInfo.Properties(attr.Name) = attr.Value
                    End If
                Next

                _elements.Add(elementInfo)
            End If

            ' Process children
            For Each childNode As XmlNode In xmlElement.ChildNodes
                If TypeOf childNode Is XmlElement Then
                    Dim childXml = DirectCast(childNode, XmlElement)
                    ' Skip property elements (e.g., Grid.RowDefinitions)
                    If Not childXml.LocalName.Contains(".") Then
                        ParseElementsRecursive(childXml)
                    End If
                End If
            Next
        End Sub

        Private Function MapElementType(elementName As String) As String
            Select Case elementName
                Case "Rectangle", "RectangleElement"
                    Return "RectangleElement"
                Case "Circle", "CircleElement"
                    Return "CircleElement"
                Case "Text", "TextElement", "TextBlock"
                    Return "TextElement"
                Case "Button"
                    Return "Button"
                Case "CheckBox"
                    Return "CheckBox"
                Case "ToggleSwitch"
                    Return "ToggleSwitch"
                Case "RadioButton"
                    Return "RadioButton"
                Case "TextBox"
                    Return "Textbox"
                Case "Slider", "Trackbar"
                    Return "Trackbar"
                Case "ProgressBar"
                    Return "ProgressBar"
                Case "StackPanel"
                    Return "StackPanel"
                Case "Grid"
                    Return "Grid"
                Case "Panel", "Border"
                    Return "RectangleElement"
                Case Else
                    Return "SceneElement"
            End Select
        End Function

        Private Function HasEvents(xmlElement As XmlElement) As Boolean
            ' Check if element has event handlers
            For Each attr As XmlAttribute In xmlElement.Attributes
                If attr.Name.StartsWith("On") OrElse
                   attr.Name = "Click" OrElse
                   attr.Name = "MouseLeftClick" OrElse
                   attr.Name = "MouseEnter" OrElse
                   attr.Name = "MouseLeave" OrElse
                   attr.Name = "DragDrop" OrElse
                   attr.Name = "MouseDrag" Then
                    Return True
                End If
            Next
            Return False
        End Function

        Private Function GenerateCode() As String
            Dim sb As New StringBuilder()

            ' File header
            sb.AppendLine("'------------------------------------------------------------------------------")
            sb.AppendLine("' <auto-generated>")
            sb.AppendLine("'     This code was generated by SceneXamlCodeGenerator.")
            sb.AppendLine("'     Changes to this file may cause incorrect behavior and will be lost if")
            sb.AppendLine("'     the code is regenerated.")
            sb.AppendLine("' </auto-generated>")
            sb.AppendLine("'------------------------------------------------------------------------------")
            sb.AppendLine()

            ' Imports
            sb.AppendLine("Imports Microsoft.Xna.Framework")
            sb.AppendLine("Imports ProjectZ.Shared.Drawing")
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI")
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI.Input")
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI.Layout")
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI.Primitives")
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.Designer")
            sb.AppendLine()

            ' Namespace
            If Not String.IsNullOrEmpty(_namespace) Then
                sb.AppendLine($"Namespace {_namespace}")
                sb.AppendLine()
            End If

            ' Partial class
            sb.AppendLine($"{_indent}Partial Public Class {_className}")
            sb.AppendLine($"{_indent}{_indent}Inherits Scene")
            sb.AppendLine()

            ' Element fields
            sb.AppendLine($"{_indent}#Region ""Designer Generated Fields""")
            sb.AppendLine()

            For Each element In _elements
                Dim modifier As String = If(element.IsWithEvents, "Friend WithEvents", "Friend")
                sb.AppendLine($"{_indent}{_indent}{modifier} {element.Name} As {element.TypeName}")
            Next

            sb.AppendLine()
            sb.AppendLine($"{_indent}#End Region")
            sb.AppendLine()

            ' InitializeComponent method
            sb.AppendLine($"{_indent}#Region ""Designer Generated Methods""")
            sb.AppendLine()
            sb.Append(GenerateInitializerCode())
            sb.AppendLine()

            ' GetXamlContent method - embeds XAML as a resource
            sb.AppendLine()
            sb.AppendLine($"{_indent}{_indent}''' <summary>")
            sb.AppendLine($"{_indent}{_indent}''' Returns the embedded XAML content for this scene.")
            sb.AppendLine($"{_indent}{_indent}''' Override this method to load XAML from a different source.")
            sb.AppendLine($"{_indent}{_indent}''' </summary>")
            sb.AppendLine($"{_indent}{_indent}Protected Overridable Function GetXamlContent() As String")
            sb.AppendLine($"{_indent}{_indent}{_indent}' In production, this would load from an embedded resource or file")
            sb.AppendLine($"{_indent}{_indent}{_indent}' For now, the XAML is embedded as a string constant")
            sb.AppendLine($"{_indent}{_indent}{_indent}Return _xamlContent")
            sb.AppendLine($"{_indent}{_indent}End Function")
            sb.AppendLine()
            sb.AppendLine($"{_indent}#End Region")
            sb.AppendLine()

            ' XAML content as embedded string
            sb.AppendLine($"{_indent}#Region ""Embedded XAML Content""")
            sb.AppendLine()
            sb.AppendLine($"{_indent}{_indent}Private Const _xamlContent As String = ")

            Dim xamlContent = File.ReadAllText(_xamlPath)
            Dim lines = xamlContent.Split({vbCrLf, vbLf}, StringSplitOptions.None)
            For i = 0 To lines.Length - 1
                Dim line = lines(i).Replace("""", """""")
                Dim continuation = If(i < lines.Length - 1, " & vbCrLf & _", "")
                sb.AppendLine($"{_indent}{_indent}{_indent}""{line}""{continuation}")
            Next

            sb.AppendLine()
            sb.AppendLine($"{_indent}#End Region")
            sb.AppendLine()

            sb.AppendLine($"{_indent}End Class")

            If Not String.IsNullOrEmpty(_namespace) Then
                sb.AppendLine()
                sb.AppendLine("End Namespace")
            End If

            Return sb.ToString()
        End Function

#End Region

    End Class

End Namespace
