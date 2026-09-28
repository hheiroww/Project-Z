using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.VisualBasic;

namespace ProjectZ.Shared.Drawing.Designer {

    /// <summary>
    /// Generates VB.NET code from XAML scene definitions.
    /// This creates designer files similar to WPF's code-behind generation.
    /// </summary>
    public class SceneXamlCodeGenerator {

        #region Fields

        private readonly string _xamlPath;
        private readonly string _namespace;
        private readonly string _className;
        private readonly List<ElementInfo> _elements = new List<ElementInfo>();
        private readonly string _indent = "    ";

        #endregion

        #region Nested Types

        private class ElementInfo {
            public string Name { get; set; }
            public string TypeName { get; set; }
            public bool IsWithEvents { get; set; } = false;
            public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a code generator for the specified XAML file.
        /// </summary>
        /// <param name="xamlPath">Path to the XAML file</param>
        /// <param name="namespace">Target namespace for generated code</param>
        /// <param name="className">Class name (typically the scene name)</param>
        public SceneXamlCodeGenerator(string xamlPath, string @namespace, string className) {
            _xamlPath = xamlPath;
            _namespace = @namespace;
            _className = className;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Generates the designer code file (.Designer.vb) content.
        /// </summary>
        public string GenerateDesignerCode() {
            string xaml = File.ReadAllText(_xamlPath);
            var doc = new XmlDocument();
            doc.LoadXml(xaml);

            // Parse all elements with x:Name
            ParseElementsRecursive(doc.DocumentElement);

            // Generate the code
            return GenerateCode();
        }

        /// <summary>
        /// Generates and writes the designer file.
        /// </summary>
        public void GenerateDesignerFile(string outputPath) {
            string code = GenerateDesignerCode();
            File.WriteAllText(outputPath, code);
        }

        /// <summary>
        /// Generates initialization code that loads XAML at runtime.
        /// </summary>
        public string GenerateInitializerCode() {
            var sb = new StringBuilder();

            sb.AppendLine($"{_indent}{_indent}''' <summary>");
            sb.AppendLine($"{_indent}{_indent}''' Initializes UI elements from XAML definition.");
            sb.AppendLine($"{_indent}{_indent}''' This is called automatically by the designer-generated code.");
            sb.AppendLine($"{_indent}{_indent}''' </summary>");
            sb.AppendLine($"{_indent}{_indent}Private Sub InitializeComponent()");
            sb.AppendLine($"{_indent}{_indent}{_indent}Dim parser As New SceneXamlParser(Me)");
            sb.AppendLine($"{_indent}{_indent}{_indent}Dim xamlContent As String = GetXamlContent()");
            sb.AppendLine($"{_indent}{_indent}{_indent}Dim root As SceneElement = parser.Parse(xamlContent)");
            sb.AppendLine();
            sb.AppendLine($"{_indent}{_indent}{_indent}' Add root element and all top-level children to the scene");
            sb.AppendLine($"{_indent}{_indent}{_indent}For Each child As SceneElement In root.Children");
            sb.AppendLine($"{_indent}{_indent}{_indent}{_indent}AddElement(child)");
            sb.AppendLine($"{_indent}{_indent}{_indent}Next");
            sb.AppendLine();
            sb.AppendLine($"{_indent}{_indent}{_indent}' Bind named elements to fields");

            foreach (var element in _elements)
                sb.AppendLine($"{_indent}{_indent}{_indent}{element.Name} = parser.FindName(Of {element.TypeName})(\"{element.Name}\")");

            sb.AppendLine($"{_indent}{_indent}End Sub");

            return sb.ToString();
        }

        #endregion

        #region Private Methods

        private void ParseElementsRecursive(XmlElement xmlElement) {
            // Check for x:Name or Name attribute
            string name = xmlElement.GetAttribute("x:Name");
            if (string.IsNullOrEmpty(name)) {
                name = xmlElement.GetAttribute("Name");
            }

            if (!string.IsNullOrEmpty(name)) {
                var elementInfo = new ElementInfo() {
                    Name = name,
                    TypeName = MapElementType(xmlElement.LocalName),
                    IsWithEvents = HasEvents(xmlElement)
                };

                // Collect properties
                foreach (XmlAttribute attr in xmlElement.Attributes) {
                    if (!attr.Name.StartsWith("xmlns") && attr.Name != "x:Name" && attr.Name != "Name") {
                        elementInfo.Properties[attr.Name] = attr.Value;
                    }
                }

                _elements.Add(elementInfo);
            }

            // Process children
            foreach (XmlNode childNode in xmlElement.ChildNodes) {
                if (childNode is XmlElement) {
                    XmlElement childXml = (XmlElement)childNode;
                    // Skip property elements (e.g., Grid.RowDefinitions)
                    if (!childXml.LocalName.Contains(".")) {
                        ParseElementsRecursive(childXml);
                    }
                }
            }
        }

        private string MapElementType(string elementName) {
            switch (elementName ?? "") {
                case "Rectangle":
                case "RectangleElement": {
                        return "RectangleElement";
                    }
                case "Circle":
                case "CircleElement": {
                        return "CircleElement";
                    }
                case "Text":
                case "TextElement":
                case "TextBlock": {
                        return "TextElement";
                    }
                case "Button": {
                        return "Button";
                    }
                case "CheckBox": {
                        return "CheckBox";
                    }
                case "RadioButton": {
                        return "RadioButton";
                    }
                case "TextBox": {
                        return "Textbox";
                    }
                case "Slider":
                case "Trackbar": {
                        return "Trackbar";
                    }
                case "ProgressBar": {
                        return "ProgressBar";
                    }
                case "StackPanel": {
                        return "StackPanel";
                    }
                case "Grid": {
                        return "Grid";
                    }
                case "Panel":
                case "Border": {
                        return "RectangleElement";
                    }

                default: {
                        return "SceneElement";
                    }
            }
        }

        private bool HasEvents(XmlElement xmlElement) {
            // Check if element has event handlers
            foreach (XmlAttribute attr in xmlElement.Attributes) {
                if (attr.Name.StartsWith("On") || attr.Name == "Click" || attr.Name == "MouseLeftClick" || attr.Name == "MouseEnter" || attr.Name == "MouseLeave" || attr.Name == "DragDrop" || attr.Name == "MouseDrag") {
                    return true;
                }
            }
            return false;
        }

        private string GenerateCode() {
            var sb = new StringBuilder();

            // File header
            sb.AppendLine("'------------------------------------------------------------------------------");
            sb.AppendLine("' <auto-generated>");
            sb.AppendLine("'     This code was generated by SceneXamlCodeGenerator.");
            sb.AppendLine("'     Changes to this file may cause incorrect behavior and will be lost if");
            sb.AppendLine("'     the code is regenerated.");
            sb.AppendLine("' </auto-generated>");
            sb.AppendLine("'------------------------------------------------------------------------------");
            sb.AppendLine();

            // Imports
            sb.AppendLine("Imports Microsoft.Xna.Framework");
            sb.AppendLine("Imports ProjectZ.Shared.Drawing");
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI");
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI.Input");
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI.Layout");
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI.Primitives");
            sb.AppendLine("Imports ProjectZ.Shared.Drawing.Designer");
            sb.AppendLine();

            // Namespace
            if (!string.IsNullOrEmpty(_namespace)) {
                sb.AppendLine($"Namespace {_namespace}");
                sb.AppendLine();
            }

            // Partial class
            sb.AppendLine($"{_indent}Partial Public Class {_className}");
            sb.AppendLine($"{_indent}{_indent}Inherits Scene");
            sb.AppendLine();

            // Element fields
            sb.AppendLine($"{_indent}#Region \"Designer Generated Fields\"");
            sb.AppendLine();

            foreach (var element in _elements) {
                string modifier = element.IsWithEvents ? "Friend WithEvents" : "Friend";
                sb.AppendLine($"{_indent}{_indent}{modifier} {element.Name} As {element.TypeName}");
            }

            sb.AppendLine();
            sb.AppendLine($"{_indent}#End Region");
            sb.AppendLine();

            // InitializeComponent method
            sb.AppendLine($"{_indent}#Region \"Designer Generated Methods\"");
            sb.AppendLine();
            sb.Append(GenerateInitializerCode());
            sb.AppendLine();

            // GetXamlContent method - embeds XAML as a resource
            sb.AppendLine();
            sb.AppendLine($"{_indent}{_indent}''' <summary>");
            sb.AppendLine($"{_indent}{_indent}''' Returns the embedded XAML content for this scene.");
            sb.AppendLine($"{_indent}{_indent}''' Override this method to load XAML from a different source.");
            sb.AppendLine($"{_indent}{_indent}''' </summary>");
            sb.AppendLine($"{_indent}{_indent}Protected Overridable Function GetXamlContent() As String");
            sb.AppendLine($"{_indent}{_indent}{_indent}' In production, this would load from an embedded resource or file");
            sb.AppendLine($"{_indent}{_indent}{_indent}' For now, the XAML is embedded as a string constant");
            sb.AppendLine($"{_indent}{_indent}{_indent}Return _xamlContent");
            sb.AppendLine($"{_indent}{_indent}End Function");
            sb.AppendLine();
            sb.AppendLine($"{_indent}#End Region");
            sb.AppendLine();

            // XAML content as embedded string
            sb.AppendLine($"{_indent}#Region \"Embedded XAML Content\"");
            sb.AppendLine();
            sb.AppendLine($"{_indent}{_indent}Private Const _xamlContent As String = ");

            string xamlContent = File.ReadAllText(_xamlPath);
            string[] lines = xamlContent.Split(new[] { Environment.NewLine, Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0, loopTo = lines.Length - 1; i <= loopTo; i++) {
                string line = lines[i].Replace("\"", "\"\"");
                string continuation = i < lines.Length - 1 ? " & vbCrLf & _" : "";
                sb.AppendLine($"{_indent}{_indent}{_indent}\"{line}\"{continuation}");
            }

            sb.AppendLine();
            sb.AppendLine($"{_indent}#End Region");
            sb.AppendLine();

            sb.AppendLine($"{_indent}End Class");

            if (!string.IsNullOrEmpty(_namespace)) {
                sb.AppendLine();
                sb.AppendLine("End Namespace");
            }

            return sb.ToString();
        }

        #endregion

    }

}