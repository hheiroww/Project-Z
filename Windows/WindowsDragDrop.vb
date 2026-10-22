Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Windows
    ''' <summary>Windows OLE drag source usable by native Project-Z hosts on an STA thread.</summary>
    Public NotInheritable Class WindowsDragDrop
        Private Sub New()
        End Sub

        Public Shared Function DoDragDrop(data As DataObject, allowedEffects As DragDropEffects) As DragDropEffects
            If data Is Nothing Then Throw New ArgumentNullException(NameOf(data))
            If Threading.Thread.CurrentThread.GetApartmentState() <> Threading.ApartmentState.STA Then Throw New InvalidOperationException("Windows drag/drop requires an STA thread.")
            Dim hr = OleInitialize(IntPtr.Zero)
            If hr < 0 Then Marshal.ThrowExceptionForHR(hr)
            Try
                ReleaseCapture()
                Dim result As Integer
                hr = NativeDoDragDrop(DirectCast(data, ComTypes.IDataObject), New DragSource(), CInt(allowedEffects), result)
                If hr < 0 Then Marshal.ThrowExceptionForHR(hr)
                GC.KeepAlive(data)
                Return If(hr = &H40101, DragDropEffects.None, CType(result, DragDropEffects))
            Finally
                OleUninitialize()
            End Try
        End Function

        <ComImport, Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
        Private Interface IDropSource
            <PreserveSig> Function QueryContinueDrag(<MarshalAs(UnmanagedType.Bool)> escapePressed As Boolean, keyState As Integer) As Integer
            <PreserveSig> Function GiveFeedback(effect As Integer) As Integer
        End Interface
        <ComVisible(True), ClassInterface(ClassInterfaceType.None)>
        Private Class DragSource
            Implements IDropSource
            Public Function QueryContinueDrag(escapePressed As Boolean, keyState As Integer) As Integer Implements IDropSource.QueryContinueDrag
                If escapePressed Then Return &H40101
                Return If((keyState And 1) = 0, &H40100, 0)
            End Function
            Public Function GiveFeedback(effect As Integer) As Integer Implements IDropSource.GiveFeedback
                Return &H40102
            End Function
        End Class
        <DllImport("ole32.dll")> Private Shared Function OleInitialize(reserved As IntPtr) As Integer
        End Function
        <DllImport("ole32.dll")> Private Shared Sub OleUninitialize()
        End Sub
        <DllImport("ole32.dll", EntryPoint:="DoDragDrop")> Private Shared Function NativeDoDragDrop(data As ComTypes.IDataObject, source As IDropSource, allowed As Integer, ByRef effect As Integer) As Integer
        End Function
        <DllImport("user32.dll")> Private Shared Function ReleaseCapture() As Boolean
        End Function
    End Class
End Namespace
