using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.Content {

    public class SpriteCollection : IDisposable {

        #region Properties

        private SortedList<int, Texture2D> Textures = new SortedList<int, Texture2D>();

        public Texture2D get_Frame(int Index) {
            return Textures.Values[Index];
        }
        public void set_Frame(int Index, Texture2D value) {
            Textures.Values[Index] = value;
        }

        public int Count {
            get {
                return Textures.Count;
            }
        }

        #endregion

        public void LoadSprite(Texture2D Texture) {
            Textures.Add(Textures.Count, Texture);
        }

        public void LoadSprite(int Index, Texture2D Texture) {
            if (Textures.ContainsKey(Index)) {
                throw new Exception("Key already exists in collection.");
            } else {
                Textures.Add(Index, Texture);
            }
        }

        public void LoadSprites(Texture2D[] Textures) {
            foreach (Texture2D Texture in Textures)
                LoadSprite(Texture);
        }

        public void RemoveSprite(int Index) {
            Textures.Remove(Index);
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    foreach (Texture2D Texture in Textures.Values)
                        Texture.Dispose();
                    Textures.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                // TODO: set large fields to null.
                Textures = null;
            }
            disposedValue = true;
        }

        // TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        // Protected Overrides Sub Finalize()
        // ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        // Dispose(False)
        // MyBase.Finalize()
        // End Sub

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }

}