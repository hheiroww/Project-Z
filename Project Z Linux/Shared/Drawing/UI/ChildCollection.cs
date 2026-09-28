using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectZ.Shared.Drawing.UI {


    public class ChildCollection : IEnumerator<SceneElement>, IEnumerable<SceneElement> {


        public event ChildAddedEventHandler ChildAdded;

        public delegate void ChildAddedEventHandler(SceneElement c);
        public event ChildRemovedEventHandler ChildRemoved;

        public delegate void ChildRemovedEventHandler(SceneElement c);

        private List<SceneElement> ChildrenList = new List<SceneElement>();

        #region Properties

        public SceneElement this[int index] {
            get {
                return ChildrenList[index];
            }
            set {
                ChildrenList[index] = value;
            }
        }

        public int Count {
            get {
                return ChildrenList.Count;
            }
        }

        private SceneElement Parent {
            get {
                return _Parent;
            }
        }
        private SceneElement _Parent;

        #endregion

        #region Iteration Support

        private int Index = -1;

        public SceneElement Current {
            get {
                return ChildrenList[Index];
            }
        }

        private object Current1 {
            get {
                return Current;
            }
        }

        object IEnumerator.Current { get => Current1; }

        public bool MoveNext() {
            if (Index < ChildrenList.Count - 1) {
                Index += 1;
                return true;
            }
            return false;
        }

        public void Reset() {
            throw new NotImplementedException();
        }

        public IEnumerator<SceneElement> GetEnumerator() {
            return ChildrenList.GetEnumerator();
        }

        private IEnumerator IEnumerable_GetEnumerator() {
            return ChildrenList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => IEnumerable_GetEnumerator();

        #endregion

        #region Public Methods

        public void ForEach(Action<SceneElement> action) {
            for (int i = ChildrenList.Count - 1; i >= 0; i -= 1)
                action.Invoke(ChildrenList[i]);
        }

        /// <summary>
        /// Adds a child element to this collection.
        /// </summary>
        public void Add(SceneElement item) {
            ChildrenList.Add(item);
            item.Parent = Parent;
            ChildAdded?.Invoke(item);
        }

        public void AddRange(IEnumerable<SceneElement> collection) {
            foreach (SceneElement Item in collection) {
                ChildrenList.Add(Item);
                Item.Parent = Parent;
                ChildAdded?.Invoke(Item);
            }
        }

        public void Insert(int index, SceneElement item) {
            ChildrenList.Insert(index, item);
            item.Parent = Parent;
            ChildAdded?.Invoke(item);
        }

        public void InsertRange(int index, IEnumerable<SceneElement> collection) {
            ChildrenList.InsertRange(index, collection);
            foreach (SceneElement Item in collection) {
                Item.Parent = Parent;
                ChildAdded?.Invoke(Item);
            }
        }

        public void Remove(SceneElement item) {
            ChildrenList.Remove(item);
            item.Parent = null;
            ChildRemoved?.Invoke(item);
        }

        public void Clear() {
            foreach (SceneElement item in ChildrenList.ToList()) {
                item.Parent = null;
                ChildRemoved?.Invoke(item);
            }
            ChildrenList.Clear();
        }

        public void RemoveAt(int Index) {
            ChildrenList[Index].Parent = null;
            ChildrenList.RemoveAt(Index);
            ChildRemoved?.Invoke(ChildrenList[Index]);
        }

        public void RemoveRange(int index, int count) {
            for (int i = index, loopTo = count; i <= loopTo; i++) {
                ChildrenList[i].Parent = null;
                ChildRemoved?.Invoke(ChildrenList[i]);
            }
            ChildrenList.RemoveRange(index, count);
        }

        public SceneElement[] ToArray() {
            return ChildrenList.ToArray();
        }

        public int IndexOf(SceneElement item) {
            return ChildrenList.IndexOf(item);
        }

        #endregion

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    ChildrenList.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                // TODO: set large fields to null.
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

        public ChildCollection(SceneElement Parent) {
            _Parent = Parent;
        }

    }

}