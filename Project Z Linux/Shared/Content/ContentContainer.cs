using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.Content {

    public class ContentContainer {
        // Implements IDisposable

        private GraphicsDevice GraphicsDevice;

        public Content.Textures TexturesInstance;

        public ContentManager Content { get; set; }

        public Dictionary<string, SpriteFont> Fonts { get; set; } = new Dictionary<string, SpriteFont>();

        public Dictionary<string, Texture2D> Textures { get; set; } = new Dictionary<string, Texture2D>();

        public List<IDictionary> AllCollections { get; set; }


        #region Content Methods

        public void LoadAllContent() {
            // #If WINDOWS Then
            string ContentDirectory = AppDomain.CurrentDomain.BaseDirectory + "Content";

            foreach (string FontDirectory in System.IO.Directory.GetDirectories(ContentDirectory + @"\Fonts")) {
                foreach (string FontPath in System.IO.Directory.GetFiles(FontDirectory)) {
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