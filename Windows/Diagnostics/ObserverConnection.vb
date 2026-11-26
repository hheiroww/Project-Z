Imports System.Diagnostics
Imports System.IO
Imports System.IO.Pipes
Imports System.Security.Principal
Imports System.Text.Json
Imports System.Threading

Namespace RuntimeDiagnostics
    Friend NotInheritable Class ObserverConnection
        Private Const StartupTimeout As Integer = 5000
        Friend Const ServerArgument As String = "--diagnostics-server"
        Friend Const PipeOptions As IO.Pipes.PipeOptions = IO.Pipes.PipeOptions.Asynchronous Or IO.Pipes.PipeOptions.CurrentUserOnly
        Friend Shared ReadOnly PipeName As String = GetPipeName()
        Friend Shared ReadOnly StartupMutexName As String = "Local\" & PipeName & ".Startup"
        Friend Shared ReadOnly InstanceMutexName As String = "Local\" & PipeName & ".Instance"

        Private Shared Function GetPipeName() As String
            Using identity = WindowsIdentity.GetCurrent(), currentProcess = Process.GetCurrentProcess()
                Return "ProjectZ.Diagnostics.1." & identity.User.Value & "." & currentProcess.SessionId
            End Using
        End Function

        Friend Shared Function TryAcquire(mutex As Mutex, milliseconds As Integer) As Boolean
            Try
                Return mutex.WaitOne(milliseconds)
            Catch ex As AbandonedMutexException
                Return True
            End Try
        End Function

        Friend Shared Function Register(helper As String, session As String, applicationName As String, silent As Boolean) As Boolean
            Dim timeout = Stopwatch.StartNew()
            Using startup As New Mutex(False, StartupMutexName), instance As New Mutex(False, InstanceMutexName),
                  client As New NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions),
                  target = Process.GetCurrentProcess()
                If Not TryAcquire(startup, StartupTimeout) Then Return False

                Try
                    Dim launched As Boolean = False
                    Do
                        Try
                            client.Connect(100)
                            Exit Do
                        Catch ex As TimeoutException
                        End Try

                        If Not launched AndAlso TryAcquire(instance, 0) Then
                            instance.ReleaseMutex()
                            Dim start As New ProcessStartInfo(helper) With {
                                .UseShellExecute = False, .CreateNoWindow = True, .WorkingDirectory = AppContext.BaseDirectory
                            }
                            start.ArgumentList.Add(ServerArgument)
                            Using observer = Process.Start(start)
                                If observer Is Nothing Then Return False
                            End Using
                            launched = True
                        End If
                    Loop While timeout.ElapsedMilliseconds < StartupTimeout
                    If Not client.IsConnected Then Return False

                    ' Write JSON directly so the protocol also works in the trimmed standalone observer.
                    Using deadline As New CancellationTokenSource(CInt(Math.Max(0L, StartupTimeout - timeout.ElapsedMilliseconds))),
                          request As New MemoryStream()
                        Using writer As New Utf8JsonWriter(request)
                            writer.WriteStartObject()
                            writer.WriteString("session", session)
                            writer.WriteString("name", applicationName)
                            writer.WriteBoolean("silent", silent)
                            writer.WriteNumber("started", target.StartTime.ToUniversalTime().Ticks)
                            writer.WriteEndObject()
                        End Using
                        request.WriteByte(10)
                        client.WriteAsync(request.GetBuffer().AsMemory(0, CInt(request.Length)), deadline.Token).AsTask().GetAwaiter().GetResult()
                        Dim response(0) As Byte
                        Return client.ReadAsync(response.AsMemory(), deadline.Token).AsTask().GetAwaiter().GetResult() = 1 AndAlso response(0) = 1
                    End Using
                Finally
                    startup.ReleaseMutex()
                End Try
            End Using
        End Function
    End Class
End Namespace
