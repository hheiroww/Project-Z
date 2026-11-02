#If PROJECTZ_DESKTOPGL Then
Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework

Namespace [Shared].Input
    ' Reuse MonoGame's SDL runtime and clipboard on the game/UI thread.
    Friend Module DesktopClipboard
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function SetTextDelegate(<MarshalAs(UnmanagedType.LPUTF8Str)> value As String) As Integer
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Function GetTextDelegate() As IntPtr
        <UnmanagedFunctionPointer(CallingConvention.Cdecl)>
        Private Delegate Sub FreeDelegate(value As IntPtr)
        Private ReadOnly api As New Lazy(Of ClipboardApi)(Function() New ClipboardApi())
        Private NotInheritable Class ClipboardApi
            Friend ReadOnly SetText As SetTextDelegate
            Friend ReadOnly GetText As GetTextDelegate
            Friend ReadOnly Free As FreeDelegate
            Friend Sub New()
                Dim library As IntPtr
                Dim names = If(OperatingSystem.IsWindows(), {"SDL2.dll"},
                    If(OperatingSystem.IsMacOS(), {"libSDL2-2.0.0.dylib"}, {"libSDL2-2.0.so.0"}))
                For Each name In names
                    If NativeLibrary.TryLoad(name, GetType(Game).Assembly, Nothing, library) Then Exit For
                Next
                If library = IntPtr.Zero Then Throw New DllNotFoundException("MonoGame's SDL2 runtime is required for the clipboard.")
                SetText = Marshal.GetDelegateForFunctionPointer(Of SetTextDelegate)(NativeLibrary.GetExport(library, "SDL_SetClipboardText"))
                GetText = Marshal.GetDelegateForFunctionPointer(Of GetTextDelegate)(NativeLibrary.GetExport(library, "SDL_GetClipboardText"))
                Free = Marshal.GetDelegateForFunctionPointer(Of FreeDelegate)(NativeLibrary.GetExport(library, "SDL_free"))
            End Sub
        End Class
        Friend Function TrySetText(text As String) As Boolean
            Return api.Value.SetText(text) = 0
        End Function
        Friend Function TryGetText(ByRef text As String) As Boolean
            Dim pointer = api.Value.GetText()
            If pointer = IntPtr.Zero Then Return False
            Try
                text = Marshal.PtrToStringUTF8(pointer)
                Return text IsNot Nothing
            Finally
                api.Value.Free(pointer)
            End Try
        End Function
    End Module
End Namespace
#End If
