Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Pipes
Imports System.Runtime.InteropServices
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.Win32.SafeHandles

Namespace RuntimeDiagnostics
    Friend Module ObserverProgram
        <STAThread>
        Sub Main(args As String())
            If args.Length <> 1 OrElse args(0) <> ObserverConnection.ServerArgument Then Return
            Using instance As New Mutex(False, ObserverConnection.InstanceMutexName)
                If Not ObserverConnection.TryAcquire(instance, 0) Then Return
                Try
                    WatchApplications()
                Finally
                    instance.ReleaseMutex()
                End Try
            End Using
        End Sub

        Private Sub WatchApplications()
            Dim watchers As New Dictionary(Of Integer, (Worker As Thread, Ready As Task))()
            Using startup As New Mutex(False, ObserverConnection.StartupMutexName)
                Do
                    Using pipe As New NamedPipeServerStream(ObserverConnection.PipeName, PipeDirection.InOut, 1,
                                                            PipeTransmissionMode.Byte, ObserverConnection.PipeOptions)
                        Dim connection = pipe.WaitForConnectionAsync()
                        While Not connection.Wait(100)
                            For Each targetId In watchers.Keys.ToArray()
                                If Not watchers(targetId).Worker.IsAlive Then watchers.Remove(targetId)
                            Next
                            If watchers.Count <> 0 OrElse Not ObserverConnection.TryAcquire(startup, 0) Then Continue While
                            Try
                                ' A client holds this mutex through registration. Close the listener under
                                ' the mutex so a late client connects only to a helper that can serve it.
                                If Not connection.IsCompleted Then
                                    pipe.Dispose()
                                    Return
                                End If
                            Finally
                                startup.ReleaseMutex()
                            End Try
                        End While
                        RegisterApplication(pipe, watchers)
                    End Using
                Loop
            End Using
        End Sub

        Private Sub RegisterApplication(pipe As NamedPipeServerStream, watchers As Dictionary(Of Integer, (Worker As Thread, Ready As Task)))
            Dim clientId As UInteger
            If Not GetNamedPipeClientProcessId(pipe.SafePipeHandle, clientId) OrElse clientId > Integer.MaxValue Then Return
            Dim targetId = CInt(clientId)
            Dim target As Process = Nothing
            Try
                Using deadline As New CancellationTokenSource(5000), reader As New StreamReader(pipe, leaveOpen:=True)
                    Dim message = reader.ReadLineAsync(deadline.Token).GetAwaiter().GetResult()
                    If message Is Nothing Then Return
                    Dim session As String
                    Dim applicationName As String
                    Dim silent As Boolean
                    Dim started As Long
                    Using registration = JsonDocument.Parse(message)
                        Dim request = registration.RootElement
                        session = request.GetProperty("session").GetString()
                        applicationName = request.GetProperty("name").GetString()
                        silent = request.GetProperty("silent").GetBoolean()
                        started = request.GetProperty("started").GetInt64()
                    End Using
                    If String.IsNullOrEmpty(session) Then Return

                    target = Process.GetProcessById(targetId)
                    ' Keep a handle to the registering process across attach and exit so a reused PID
                    ' cannot transfer the registration to another process.
                    If target.SafeHandle.IsInvalid OrElse target.HasExited OrElse
                        target.StartTime.ToUniversalTime().Ticks <> started Then Return

                    Dim watcher As (Worker As Thread, Ready As Task) = Nothing
                    If Not watchers.TryGetValue(targetId, watcher) OrElse Not watcher.Worker.IsAlive Then
                        Dim ready As New TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
                        Dim observedTarget = target
                        Dim observer As New NativeObserver(observedTarget, session, applicationName, silent)
                        Dim worker As New Thread(Sub()
                                                     Using observedTarget
                                                         observer.Watch(Sub() ready.TrySetResult())
                                                     End Using
                                                 End Sub) With {.IsBackground = True, .Name = "Project-Z crash observer"}
                        worker.Start()
                        target = Nothing
                        watcher = (worker, ready.Task)
                        watchers(targetId) = watcher
                    End If
                    watcher.Ready.WaitAsync(deadline.Token).GetAwaiter().GetResult()
                    pipe.WriteAsync(New Byte() {1}.AsMemory(), deadline.Token).AsTask().GetAwaiter().GetResult()
                End Using
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is JsonException OrElse TypeOf ex Is KeyNotFoundException OrElse TypeOf ex Is FormatException OrElse
                TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is ArgumentException OrElse
                TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is Win32Exception
                ' A closing or invalid client must leave other applications under observation.
            Finally
                target?.Dispose()
            End Try
        End Sub

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Function GetNamedPipeClientProcessId(pipe As SafePipeHandle, ByRef clientId As UInteger) As Boolean
        End Function
    End Module
End Namespace
