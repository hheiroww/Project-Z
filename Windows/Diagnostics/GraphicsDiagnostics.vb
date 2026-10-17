Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.CompilerServices
Imports Microsoft.Xna.Framework.Graphics

Namespace RuntimeDiagnostics
Public NotInheritable Class GraphicsDiagnostics
    Private Shared ReadOnly devices As New ConditionalWeakTable(Of GraphicsDevice, Object)()

    Public Shared Sub Register(device As GraphicsDevice)
        If device Is Nothing Then Return
        devices.GetValue(device, Function(key)
                                    Try
                                        ApplicationDiagnostics.Log("Graphics backend=" & Global.ProjectZ.Shared.Drawing.GraphicsBackendInfo.Current.ToString() &
                                            "; adapter=" & key.Adapter.Description & "; profile=" & key.GraphicsProfile.ToString())
                                        Using currentProcess = Global.System.Diagnostics.Process.GetCurrentProcess()
                                            For Each loaded As ProcessModule In currentProcess.Modules
                                                If String.Equals(loaded.ModuleName, "mgruntime.dll", StringComparison.OrdinalIgnoreCase) Then
                                                    ApplicationDiagnostics.Log("Native graphics runtime=" & loaded.FileName & "; SHA256=" &
                                                        Convert.ToHexString(Security.Cryptography.SHA256.HashData(File.ReadAllBytes(loaded.FileName))))
                                                End If
                                            Next
                                        End Using
                                        AddHandler key.DeviceResetting, Sub(sender, args) ApplicationDiagnostics.Log("Graphics device resetting")
                                        AddHandler key.DeviceReset, Sub(sender, args) ApplicationDiagnostics.Log("Graphics device reset completed")
                                        AddHandler key.Disposing, Sub(sender, args) ApplicationDiagnostics.Log("Graphics device disposing")
                                    Catch failure As Exception
                                        ApplicationDiagnostics.Report("Graphics diagnostic metadata", failure)
                                    End Try
                                    Return New Object()
                                End Function)
    End Sub
End Class
End Namespace
