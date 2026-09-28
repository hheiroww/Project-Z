Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Input
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Animations.Properties
Imports System.Collections.Generic

Imports ProjectZ.Shared.XNA
Imports System.Collections

Namespace [Shared].Drawing.UI

    Public Class ChildCollection

        Implements IEnumerator(Of SceneElement)
        Implements IEnumerable(Of SceneElement)


        Public Event ChildAdded(c As SceneElement)
        Public Event ChildRemoved(c As SceneElement)

        Private ChildrenList As New List(Of SceneElement)

#Region "Properties"

        Default Public Property Item(index As Integer) As SceneElement
            Get
                Return ChildrenList.Item(index)
            End Get
            Set(value As SceneElement)
                ChildrenList.Item(index) = value
            End Set
        End Property

        Public ReadOnly Property Count As Integer
            Get
                Return ChildrenList.Count
            End Get
        End Property

        Private ReadOnly Property Parent As SceneElement
            Get
                Return _Parent
            End Get
        End Property
        Private _Parent As SceneElement

#End Region

#Region "Iteration Support"

        Private Index As Integer = -1

        Public ReadOnly Property Current As SceneElement Implements System.Collections.Generic.IEnumerator(Of SceneElement).Current
            Get
                Return ChildrenList(Index)
            End Get
        End Property

        Private ReadOnly Property Current1 As Object Implements System.Collections.IEnumerator.Current
            Get
                Return Me.Current
            End Get
        End Property

        Public Function MoveNext() As Boolean Implements System.Collections.IEnumerator.MoveNext
            If Index < ChildrenList.Count - 1 Then
                Index += 1
                Return True
            End If
            Return False
        End Function

        Public Sub Reset() Implements System.Collections.IEnumerator.Reset
            Throw New NotImplementedException
        End Sub

        Public Function GetEnumerator() As IEnumerator(Of SceneElement) Implements IEnumerable(Of SceneElement).GetEnumerator
            Return ChildrenList.GetEnumerator()
        End Function

        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return ChildrenList.GetEnumerator()
        End Function

#End Region

#Region "Public Methods"

        Public Sub ForEach(action As Action(Of SceneElement))
            For i As Integer = ChildrenList.Count - 1 To 0 Step -1
                action.Invoke(ChildrenList(i))
            Next
        End Sub

        ''' <summary>
        ''' Adds a child element to this collection.
        ''' </summary>
        Public Sub Add(item As SceneElement)
            ChildrenList.Add(item)
            item.Parent = Parent
            RaiseEvent ChildAdded(item)
        End Sub

        Public Sub AddRange(collection As IEnumerable(Of SceneElement))
            For Each Item As SceneElement In collection
                ChildrenList.Add(Item)
                Item.Parent = Parent
                RaiseEvent ChildAdded(Item)
            Next
        End Sub

        Public Sub Insert(index As Integer, item As SceneElement)
            ChildrenList.Insert(index, item)
            item.Parent = Parent
            RaiseEvent ChildAdded(item)
        End Sub

        Public Sub InsertRange(index As Integer, collection As IEnumerable(Of SceneElement))
            ChildrenList.InsertRange(index, collection)
            For Each Item As SceneElement In collection
                Item.Parent = Parent
                RaiseEvent ChildAdded(Item)
            Next
        End Sub

        Public Sub Remove(item As SceneElement)
            If Not ChildrenList.Remove(item) Then Return
            item.Parent = Nothing
            RaiseEvent ChildRemoved(item)
        End Sub

        Public Sub Clear()
            For Each item As SceneElement In ChildrenList.ToList()
                item.Parent = Nothing
                RaiseEvent ChildRemoved(item)
            Next
            ChildrenList.Clear()
        End Sub

        Public Sub RemoveAt(Index As Integer)
            Dim removed = ChildrenList.Item(Index)
            removed.Parent = Nothing
            ChildrenList.RemoveAt(Index)
            RaiseEvent ChildRemoved(removed)
        End Sub

        Public Sub RemoveRange(index As Integer, count As Integer)
            Dim removed = ChildrenList.GetRange(index, count)
            ChildrenList.RemoveRange(index, count)
            For Each child As SceneElement In removed
                child.Parent = Nothing
                RaiseEvent ChildRemoved(child)
            Next
        End Sub

        Public Function ToArray() As SceneElement()
            Return ChildrenList.ToArray
        End Function

        Public Function IndexOf(item As SceneElement) As Integer
            Return ChildrenList.IndexOf(item)
        End Function

#End Region

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                    ChildrenList.Clear()
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TODO: set large fields to null.
            End If
            Me.disposedValue = True
        End Sub

        ' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        'Protected Overrides Sub Finalize()
        '    ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
        '    Dispose(False)
        '    MyBase.Finalize()
        'End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region

        Public Sub New(Parent As SceneElement)
            _Parent = Parent
        End Sub

    End Class

End Namespace
