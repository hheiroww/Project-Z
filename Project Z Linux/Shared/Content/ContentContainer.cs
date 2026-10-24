using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.Content {

    public class ContentContainer {
        // Implements IDisposable

        private GraphicsDevice GraphicsDevice;

        public Content.Textures TexturesInstance;

        public ContentManager Content { get; set; }

        public Dictionary<string, SpriteFont> Fonts { get; set; } = new Dictionary<string, SpriteFont>();
        public Dictionary<string, VectorFont> VectorFonts { get; } = new Dictionary<string, VectorFont>();

        public void LoadVectorFontsOnly() {
            foreach (int size in new[] { 10, 12, 14, 18, 24, 36, 48, 72 }) TryGetVectorFont("ProjectZSans_" + size, out _);
        }

        public bool TryGetVectorFont(string name, out VectorFont font) {
            font = null!;
            if (string.IsNullOrEmpty(name)) return false;
            if (VectorFonts.TryGetValue(name, out font)) return true;
            string prefix = name.StartsWith("ProjectZSans_", StringComparison.Ordinal) ? "ProjectZSans_" : "SegoeUI_";
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !float.TryParse(name.Substring(prefix.Length), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float size)
                || float.IsNaN(size) || float.IsInfinity(size) || size <= 0 || size > 512) return false;
            VectorFonts.Add(name, font = new VectorFont(size));
            return true;
        }

        public Dictionary<string, Texture2D> Textures { get; set; } = new Dictionary<string, Texture2D>();

        public List<IDictionary> AllCollections { get; set; }


        #region Content Methods

        public void LoadAllContent() {
            LoadVectorFontsOnly();
            // #If WINDOWS Then
            string ContentDirectory = AppDomain.CurrentDomain.BaseDirectory + "Content";

            string fontsDirectory = Path.Combine(ContentDirectory, "Fonts");
            if (!Directory.Exists(fontsDirectory)) return;
            foreach (string FontDirectory in System.IO.Directory.GetDirectories(fontsDirectory)) {
                foreach (string FontPath in System.IO.Directory.GetFiles(FontDirectory)) {
                    if (Path.GetFileName(FontPath).StartsWith("SegoeUI_", StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetExtension(FontPath), ".xnb", StringComparison.OrdinalIgnoreCase)) continue;
                    string RelativeFontPath = FontPath.Remove(0, ContentDirectory.Length + 1);
                    string Extension = System.IO.Path.GetExtension(RelativeFontPath);
                    var Font = Content.Load<SpriteFont>(RelativeFontPath.Replace(Extension, null));
                    Fonts.Add(System.IO.Path.GetFileNameWithoutExtension(FontPath), Font);
                }
            }
            // #End If
        }
        // Removed GetContentResource (Windows-specific resource loading)
        #endregion

        public ContentContainer(ContentManager Content, GraphicsDevice GraphicsDevice) {
            this.Content = Content;
            this.GraphicsDevice = GraphicsDevice;
            TexturesInstance = new Content.Textures(GraphicsDevice);
            AllCollections = new List<IDictionary>() { Fonts, Textures };
        }


        // #Region "IDisposable Support"
        // Private disposedValue As Boolean ' To detect redundant calls

        // ' IDisposable
        // Protected Overridable Sub Dispose(disposing As Boolean)
        // If Not disposedValue Then
        // If disposing Then
        // ' TODO: dispose managed state (managed objects).
        // For Each Texture As Texture2D In Textures.Values
        // Texture.Dispose()
        // Next
        // Content.Dispose()
        // End If

        // ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
        // ' TODO: set large fields to null.
        // Fonts = Nothing
        // Textures = Nothing
        // AllCollections = Nothing
        // End If
        // disposedValue = True
        // End Sub

        // ' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        // 'Protected Overrides Sub Finalize()
        // '    ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        // '    Dispose(False)
        // '    MyBase.Finalize()
        // 'End Sub

        // ' This code added by Visual Basic to correctly implement the disposable pattern.
        // Public Sub Dispose() Implements IDisposable.Dispose
        // ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        // Dispose(True)
        // GC.SuppressFinalize(Me)
        // End Sub
        // #End Region

    }

}
