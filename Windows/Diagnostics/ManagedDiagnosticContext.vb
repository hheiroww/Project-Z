Imports System.IO

Namespace RuntimeDiagnostics
    Friend Module ManagedDiagnosticContext
        Friend Const MapCapacity As Integer = 262144
        Friend Const SlotCapacity As Integer = (MapCapacity - 16) \ 2

        Friend Function MapName(session As String) As String
            Return "Local\ProjectZ.Managed." & Path.GetFileNameWithoutExtension(session)
        End Function
    End Module
End Namespace
