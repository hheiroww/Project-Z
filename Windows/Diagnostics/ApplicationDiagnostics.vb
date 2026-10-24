Imports System.Diagnostics
Imports System.IO
Imports System.IO.MemoryMappedFiles
Imports System.Runtime.InteropServices
Imports System.Text

Namespace RuntimeDiagnostics
' The observer runs outside the UI process so native faults and forced exits
' cannot take the log writer down with them. It observes second-chance faults;
' it never consumes application exceptions or tries to resume a damaged process.
Public NotInheritable Class ApplicationDiagnostics
#If PROJECTZ_DIAGNOSTICS_OBSERVER Then
    <DllImport("user32.dll", EntryPoint:="MessageBoxW", CharSet:=CharSet.Unicode)>
    Private Shared Function ObserverMessageBox(owner As IntPtr, text As String, caption As String, flags As UInteger) As Integer
    End Function
#End If
    Private Shared ReadOnly gate As New Object()
    Public Shared Property SessionPath As String
    Private Shared diagnosticFolder As String
    Private Shared displayName As String
    Private Shared initialized As Integer
    Private Shared ReadOnly recentManaged As New Dictionary(Of UInteger, String)()
    Private Shared managedMap As MemoryMappedFile
    Private Shared managedView As MemoryMappedViewAccessor
    Private Const ManagedMapCapacity As Integer = 262144
    Private Const ManagedSlotCapacity As Integer = (ManagedMapCapacity - 16) \ 2
    Private Shared managedRevision As Long
    Private Shared writtenManagedRevision As Long
    Private Shared managedWriterBusy As Integer
    Private Shared managedFlushTimer As Threading.Timer
    <ThreadStatic> Private Shared capturingManaged As Boolean
    Public Shared ReadOnly Property Folder As String
        Get
            Return diagnosticFolder
        End Get
    End Property

    Public Shared Sub Initialize(Optional options As DiagnosticOptions = Nothing)
        If Threading.Interlocked.Exchange(initialized, 1) <> 0 Then Return
        options = If(options, New DiagnosticOptions())
        Dim entry = Reflection.Assembly.GetEntryAssembly()
        displayName = If(options.ApplicationName, If(entry?.GetName().Name, "Project-Z application"))
        Dim safeName = String.Concat(displayName.Select(Function(ch) If(Path.GetInvalidFileNameChars().Contains(ch), "_"c, ch)))
        diagnosticFolder = If(options.DirectoryPath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Project-Z", safeName, "Diagnostics"))
        Dim silent = Not options.ShowErrorDialogs
        Try
            Directory.CreateDirectory(folder)
            SessionPath = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") & "-" & Environment.ProcessId & ".log")
            Try
                managedMap = MemoryMappedFile.CreateOrOpen(ManagedMapName(SessionPath), ManagedMapCapacity, MemoryMappedFileAccess.ReadWrite)
                managedView = managedMap.CreateViewAccessor()
            Catch ex As Exception
                Log("Managed context shared memory unavailable: " & ex.GetType().Name)
            End Try
            Log("Start; executable=" & Environment.ProcessPath & "; version=" & If(entry?.GetName().Version?.ToString(), "unknown") &
                "; runtime=" & RuntimeInformation.FrameworkDescription & "; OS=" & RuntimeInformation.OSDescription)
            Dim marker = Path.Combine(AppContext.BaseDirectory, "ProjectZ.Release.json")
            If IO.File.Exists(marker) Then Log("Release=" & IO.File.ReadAllText(marker).Trim())
            Log("Application SHA256=" & Convert.ToHexString(Security.Cryptography.SHA256.HashData(IO.File.ReadAllBytes(entry.Location))))
            For Each old In New DirectoryInfo(folder).GetFiles("*.log").OrderByDescending(Function(file) file.LastWriteTimeUtc).Skip(20)
                Try
                    old.Delete()
                    Dim snapshot = old.FullName & ".managed.txt"
                    If File.Exists(snapshot) Then File.Delete(snapshot)
                Catch
                End Try
            Next
            Dim helper = Path.Combine(Path.GetDirectoryName(GetType(ApplicationDiagnostics).Assembly.Location), "ProjectZ.Diagnostics.exe")
            Dim start As New ProcessStartInfo(helper) With {.UseShellExecute = False, .CreateNoWindow = True, .WindowStyle = ProcessWindowStyle.Hidden, .WorkingDirectory = AppContext.BaseDirectory}
            start.ArgumentList.Add("--diagnostics-watch")
            start.ArgumentList.Add(Environment.ProcessId.ToString())
            start.ArgumentList.Add("--diagnostics-session")
            start.ArgumentList.Add(SessionPath)
            start.ArgumentList.Add("--application-name")
            start.ArgumentList.Add(displayName)
            If silent Then start.ArgumentList.Add("--diagnostics-no-dialog")
            Using observer = Process.Start(start)
                Dim timeout = Stopwatch.StartNew()
                While Not IO.File.Exists(SessionPath & ".ready") AndAlso Not observer.HasExited AndAlso timeout.ElapsedMilliseconds < 5000
                    Threading.Thread.Sleep(20)
                End While
            End Using
            If IO.File.Exists(SessionPath & ".ready") Then
                IO.File.Delete(SessionPath & ".ready")
            Else
                Log("WARNING: Native crash observer did not acknowledge startup.")
            End If
        Catch ex As Exception
            Log("Diagnostic initialization failed: " & ex.ToString())
        End Try
        AddHandler AppDomain.CurrentDomain.UnhandledException, Sub(sender, args) Report("Unhandled; terminating=" & args.IsTerminating, TryCast(args.ExceptionObject, Exception))
        ' A native debugger can see a fatal CLR event before UnhandledException.
        ' Publish bounded context to shared memory; only the background writer
        ' and external observer touch disk for ordinary first-chance snapshots.
        AddHandler AppDomain.CurrentDomain.FirstChanceException, Sub(sender, args) CaptureManagedContext(args.Exception)
        managedFlushTimer = New Threading.Timer(Sub(state) FlushManagedContext(), Nothing, 250, 250)
        AddHandler TaskScheduler.UnobservedTaskException, Sub(sender, args) Report("Unobserved task", args.Exception)
#If Not PROJECTZ_DIAGNOSTICS_OBSERVER Then
        AddHandler System.Windows.Forms.Application.ThreadException, Sub(sender, args)
                                                                         Report("Windows message callback", args.Exception)
                                                                         Environment.FailFast("Unhandled Windows message callback", args.Exception)
                                                                     End Sub
#End If
        AddHandler AppDomain.CurrentDomain.ProcessExit, Sub(sender, args) Log("ProcessExit; code=" & Environment.ExitCode)
    End Sub

    Public Shared Sub Report(context As String, failure As Exception)
        FlushManagedContext()
        Log(context & Environment.NewLine & If(failure?.ToString(), "No managed exception details available"))
    End Sub

    Private Shared Sub CaptureManagedContext(failure As Exception)
        If capturingManaged OrElse String.IsNullOrEmpty(SessionPath) Then Return
        capturingManaged = True
        Try
            Dim threadId = GetCurrentThreadId()
            Dim detail = DateTime.UtcNow.ToString("O") & " | OS thread=" & threadId & Environment.NewLine & failure.ToString()
            SyncLock recentManaged
                recentManaged.Remove(threadId)
                recentManaged.Add(threadId, detail)
                While recentManaged.Count > 8
                    recentManaged.Remove(recentManaged.Keys.First())
                End While
                managedRevision += 1
                If managedView IsNot Nothing Then
                    Dim snapshot = detail & Environment.NewLine & Environment.NewLine & String.Join(Environment.NewLine & Environment.NewLine, recentManaged.Where(Function(entry) entry.Key <> threadId).Select(Function(entry) entry.Value))
                    Dim bytes = Encoding.UTF8.GetBytes(snapshot.Substring(0, Math.Min(snapshot.Length, 32000)))
                    Dim slot = 1 - managedView.ReadInt32(0)
                    Dim at = 16L + slot * ManagedSlotCapacity
                    managedView.WriteArray(at + 4, bytes, 0, bytes.Length)
                    managedView.Write(at, bytes.Length)
                    Threading.Thread.MemoryBarrier()
                    managedView.Write(0, slot)
                End If
            End SyncLock
        Catch
        Finally
            capturingManaged = False
        End Try
    End Sub

    ' Disk snapshots run in the background without holding the producer lock.
    ' The double-buffered shared-memory copy remains readable even if a native
    ' fatal event suspends every application thread before this timer runs.
    Private Shared Sub FlushManagedContext()
        If capturingManaged OrElse String.IsNullOrEmpty(SessionPath) OrElse Threading.Interlocked.CompareExchange(managedWriterBusy, 1, 0) <> 0 Then Return
        capturingManaged = True
        Try
            Dim snapshot As String()
            Dim revision As Long
            SyncLock recentManaged
                revision = managedRevision
                If revision = writtenManagedRevision Then Return
                snapshot = recentManaged.Values.ToArray()
            End SyncLock
            Dim path = SessionPath & ".managed.txt"
            File.WriteAllText(path & ".new", "Recent managed exception context; these exceptions may have been handled." & Environment.NewLine & String.Join(Environment.NewLine & Environment.NewLine, snapshot))
            File.Move(path & ".new", path, True)
            writtenManagedRevision = revision
        Catch
        Finally
            capturingManaged = False
            Threading.Interlocked.Exchange(managedWriterBusy, 0)
        End Try
    End Sub

    Private Shared Function ManagedMapName(session As String) As String
        Return "Local\ProjectZ.Managed." & Path.GetFileNameWithoutExtension(session)
    End Function

    Private Shared Function ReadManagedMap(session As String) As String
        Try
            Using map = MemoryMappedFile.OpenExisting(ManagedMapName(session), MemoryMappedFileRights.Read), view = map.CreateViewAccessor(0, ManagedMapCapacity, MemoryMappedFileAccess.Read)
                Dim slot = view.ReadInt32(0)
                If slot < 0 OrElse slot > 1 Then Return String.Empty
                Dim at = 16L + slot * ManagedSlotCapacity
                Dim length = view.ReadInt32(at)
                If length <= 0 OrElse length > ManagedSlotCapacity - 4 Then Return String.Empty
                Dim bytes(length - 1) As Byte
                view.ReadArray(at + 4, bytes, 0, bytes.Length)
                Return Encoding.UTF8.GetString(bytes)
            End Using
        Catch
            Return String.Empty
        End Try
    End Function


    Public Shared Sub Log(message As String)
        If String.IsNullOrEmpty(SessionPath) Then Return
        Try
            SyncLock gate
                ' Allow the native observer to append to the same durable session.
                Using stream As New FileStream(SessionPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), writer As New StreamWriter(stream, New UTF8Encoding(False))
                    writer.WriteLine(DateTime.UtcNow.ToString("O") & " | pid=" & Environment.ProcessId & " thread=" & Environment.CurrentManagedThreadId & " | " & message)
                    writer.Flush()
                    stream.Flush(True)
                End Using
            End SyncLock
        Catch
            ' Logging must not create another application failure.
        End Try
    End Sub

    Friend Shared Sub Watch(targetId As Integer, session As String, silent As Boolean, applicationName As String)
        diagnosticFolder = Path.GetDirectoryName(session)
        displayName = applicationName
        SessionPath = Path.ChangeExtension(session, ".observer.log")
        Dim nativeFault = False
        Dim graphicsOriginCaptured = False
        Dim attachBreakpoint = True
        Dim events = Marshal.AllocHGlobal(176) ' Windows x64 DEBUG_EVENT
        Try
            Using target = Process.GetProcessById(targetId)
                If Not DebugActiveProcess(CUInt(targetId)) Then
                    Log("Native observer attach failed; Win32=" & Marshal.GetLastWin32Error())
                    IO.File.WriteAllText(session & ".ready", "failed")
                    target.WaitForExit()
                    Log("Observed exit without debugger; code=" & target.ExitCode.ToString("X8"))
                    Return
                End If
                DebugSetProcessKillOnExit(False)
                Log("Native observer attached; target=" & targetId)
                IO.File.WriteAllText(session & ".ready", "ready")
                Do
                    If Not WaitForDebugEvent(events, 1000UI) Then
                        If target.HasExited Then Exit Do
                        Continue Do
                    End If
                    Dim kind = Marshal.ReadInt32(events, 0)
                    Dim processId = CUInt(Marshal.ReadInt32(events, 4))
                    Dim threadId = CUInt(Marshal.ReadInt32(events, 8))
                    Dim status As UInteger = &H10002UI ' DBG_CONTINUE for lifecycle events
                    If kind = 1 Then
                        Dim code = Marshal.ReadInt32(events, 16)
                        Dim firstChance = Marshal.ReadInt32(events, 168) <> 0
                        If firstChance AndAlso code = &HE06D7363 Then
                            Dim graphicsError = ReadGraphicsException(target, events)
                            If graphicsError IsNot Nothing Then
                                Log("Native graphics exception before cleanup; OS thread=" & threadId & "; " & graphicsError)
                                If Not graphicsOriginCaptured Then
                                    graphicsOriginCaptured = True
                                    WriteDump(target, Path.ChangeExtension(session, ".native-origin.log"), events, threadId)
                                End If
                            End If
                        End If
                        ' Let application/runtime handlers see every exception,
                        ' except the debugger's initial attach breakpoint.
                        If code = &H80000003 AndAlso attachBreakpoint Then
                            attachBreakpoint = False
                        Else
                            status = &H80010001UI
                        End If
                        If Not firstChance Then
                            nativeFault = True
                            Log("FATAL native exception; code=0x" & code.ToString("X8") & "; address=0x" & Marshal.ReadInt64(events, 32).ToString("X") & "; OS thread=" & threadId)
                            If code = &HC0000409 AndAlso Marshal.ReadInt32(events, 40) > 0 Then Log("Fast-fail reason=" & Marshal.ReadInt64(events, 48))
                            If code = &HE0434352 OrElse graphicsOriginCaptured Then
                                Dim snapshot = session & ".managed.txt"
                                Dim context = ReadManagedMap(session)
                                If String.IsNullOrEmpty(context) AndAlso File.Exists(snapshot) Then context = File.ReadAllText(snapshot)
                                If Not String.IsNullOrEmpty(context) Then Log("Managed exception context before fatal CLR event:" & Environment.NewLine & context)
                            End If
                            Dim address = Marshal.ReadInt64(events, 32)
                            For Each loaded As ProcessModule In target.Modules
                                Dim start = loaded.BaseAddress.ToInt64()
                                If address >= start AndAlso address - start < loaded.ModuleMemorySize Then
                                    Log("Fault module=" & loaded.FileName & "; offset=0x" & (address - start).ToString("X"))
                                    Exit For
                                End If
                            Next
                            WriteDump(target, session, events, threadId)
                        End If
                    ElseIf kind = 3 Then
                        CloseDebugHandle(events, 16) ' CREATE_PROCESS file
                        ' Windows owns debug-event process/thread handles and closes
                        ' them when their exit events are continued. Closing them
                        ' here lets that later close invalidate an unrelated reused
                        ' handle, including Process.HasExited's wait handle.
                    ElseIf kind = 6 Then
                        CloseDebugHandle(events, 16) ' LOAD_DLL file
                    ElseIf kind = 8 Then
                        Dim unicode = Marshal.ReadInt16(events, 24) <> 0
                        Dim length = Math.Min(CInt(CUShort(Marshal.ReadInt16(events, 26))), 2048)
                        Dim bytes = ReadRemoteBytes(target, Marshal.ReadInt64(events, 16), length * If(unicode, 2, 1))
                        If bytes IsNot Nothing Then
                            Dim message = If(unicode, Encoding.Unicode, Encoding.UTF8).GetString(bytes).TrimEnd(ChrW(0), ChrW(10), ChrW(13))
                            If message.StartsWith("Project-Z DX12:", StringComparison.Ordinal) Then Log(message)
                        End If
                    ElseIf kind = 5 Then
                        Log("Observed process exit; target=" & targetId & "; code=0x" & Marshal.ReadInt32(events, 16).ToString("X8"))
                    End If
                    ContinueDebugEvent(processId, threadId, status)
                    If kind = 5 Then Exit Do
                Loop
            End Using
        Catch ex As Exception
            Report("Native observer failed", ex)
        Finally
            DebugActiveProcessStop(CUInt(targetId))
            Marshal.FreeHGlobal(events)
        End Try
        If nativeFault AndAlso Not silent Then
#If PROJECTZ_DIAGNOSTICS_OBSERVER Then
            ObserverMessageBox(IntPtr.Zero, displayName & " closed because of a native error. Diagnostic logs and a crash dump were saved in:" & Environment.NewLine & Folder, displayName & " error", &H10UI)
#Else
            System.Windows.Forms.MessageBox.Show(displayName & " closed because of a native error. Diagnostic logs and a crash dump were saved in:" & Environment.NewLine & Folder,
                                                 displayName & " error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error)
#End If
        End If
    End Sub

    Private Shared Function ReadRemoteBytes(target As Process, address As Long, count As Integer) As Byte()
        If address <= 0 OrElse count <= 0 OrElse count > 4096 Then Return Nothing
        Dim bytes(count - 1) As Byte
        Dim read As IntPtr
        If Not ReadProcessMemory(target.Handle, New IntPtr(address), bytes, New IntPtr(count), read) OrElse read.ToInt64() <> count Then Return Nothing
        Return bytes
    End Function

    Private Shared Function ReadGraphicsException(target As Process, events As IntPtr) As String
        ' MSVC x64 C++ exception metadata uses image-relative RTTI offsets.
        ' Validate the exact DX exception type before interpreting its HRESULT.
        Try
            If Marshal.ReadInt32(events, 40) <> 4 Then Return Nothing
            Dim origin = Marshal.ReadInt64(events, 72)
            Dim info = ReadRemoteBytes(target, Marshal.ReadInt64(events, 64), 16)
            If info Is Nothing Then Return Nothing
            Dim types = ReadRemoteBytes(target, origin + BitConverter.ToUInt32(info, 12), 8)
            If types Is Nothing OrElse BitConverter.ToInt32(types, 0) <= 0 Then Return Nothing
            Dim typeInfo = ReadRemoteBytes(target, origin + BitConverter.ToUInt32(types, 4), 28)
            If typeInfo Is Nothing OrElse BitConverter.ToInt32(typeInfo, 20) <> 32 Then Return Nothing
            Dim descriptor = ReadRemoteBytes(target, origin + BitConverter.ToUInt32(typeInfo, 4), 128)
            If descriptor Is Nothing Then Return Nothing
            Dim name = Encoding.ASCII.GetString(descriptor, 16, 112).Split(ChrW(0))(0)
            If name <> ".?AVcom_exception@DX@@" Then Return Nothing
            Dim value = ReadRemoteBytes(target, Marshal.ReadInt64(events, 56), 32)
            If value Is Nothing Then Return Nothing
            Return "type=DX::com_exception; HRESULT=0x" & BitConverter.ToUInt32(value, 24).ToString("X8")
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Sub CloseDebugHandle(events As IntPtr, offset As Integer)
        Dim handle = Marshal.ReadIntPtr(events, offset)
        If handle <> IntPtr.Zero AndAlso handle <> New IntPtr(-1) Then CloseHandle(handle)
    End Sub

    Private Shared Sub WriteDump(target As Process, session As String, events As IntPtr, threadId As UInteger)
        Dim contextBuffer = Marshal.AllocHGlobal(1248)
        Dim context = New IntPtr((contextBuffer.ToInt64() + 15L) And Not 15L)
        Dim pointers = Marshal.AllocHGlobal(16)
        Dim exceptionInfo = Marshal.AllocHGlobal(16) ' MINIDUMP headers use pack(4)
        Dim thread = OpenThread(&H48UI, False, threadId)
        Try
            For Each old In New DirectoryInfo(folder).GetFiles("*.dmp").OrderByDescending(Function(file) file.LastWriteTimeUtc).Skip(2)
                old.Delete()
            Next
            Using output As New FileStream(Path.ChangeExtension(session, ".dmp"), FileMode.Create, FileAccess.Write, FileShare.Read)
                ' Include thread stacks, module metadata and unloaded modules.
                ' Full process memory (chat contents/credentials) is not requested.
                Marshal.Copy(New Byte(1231) {}, 0, context, 1232)
                Marshal.WriteInt32(context, 48, &H10001F) ' x64 CONTEXT_ALL
                Dim info = IntPtr.Zero
                If thread <> IntPtr.Zero AndAlso GetThreadContext(thread, context) Then
                    Marshal.WriteIntPtr(pointers, 0, IntPtr.Add(events, 16)) ' local EXCEPTION_RECORD64
                    Marshal.WriteIntPtr(pointers, 8, context)
                    Marshal.WriteInt32(exceptionInfo, 0, CInt(threadId))
                    Marshal.WriteIntPtr(exceptionInfo, 4, pointers)
                    Marshal.WriteInt32(exceptionInfo, 12, 0) ' pointers belong to observer
                    info = exceptionInfo
                Else
                    Log("Exception context unavailable; Win32=" & Marshal.GetLastWin32Error())
                End If
                If Not MiniDumpWriteDump(target.Handle, CUInt(target.Id), output.SafeFileHandle.DangerousGetHandle(), &H1820UI, info, IntPtr.Zero, IntPtr.Zero) Then
                    Log("Crash dump failed; Win32=" & Marshal.GetLastWin32Error())
                Else
                    Log("Crash dump saved: " & output.Name)
                End If
            End Using
        Catch ex As Exception
            Report("Crash dump failed", ex)
        Finally
            If thread <> IntPtr.Zero Then CloseHandle(thread)
            Marshal.FreeHGlobal(exceptionInfo)
            Marshal.FreeHGlobal(pointers)
            Marshal.FreeHGlobal(contextBuffer)
        End Try
    End Sub

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function GetCurrentThreadId() As UInteger
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function ReadProcessMemory(process As IntPtr, address As IntPtr, <Out> buffer As Byte(), size As IntPtr, ByRef read As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function DebugActiveProcess(processId As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function DebugActiveProcessStop(processId As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function DebugSetProcessKillOnExit(kill As Boolean) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function WaitForDebugEvent(debugEvent As IntPtr, milliseconds As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function ContinueDebugEvent(processId As UInteger, threadId As UInteger, status As UInteger) As Boolean
    End Function
    <DllImport("kernel32.dll")>
    Private Shared Function CloseHandle(handle As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function OpenThread(access As UInteger, inherit As Boolean, threadId As UInteger) As IntPtr
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function GetThreadContext(thread As IntPtr, context As IntPtr) As Boolean
    End Function
    <DllImport("dbghelp.dll", SetLastError:=True)>
    Private Shared Function MiniDumpWriteDump(process As IntPtr, processId As UInteger, file As IntPtr, dumpType As UInteger, exception As IntPtr, userStream As IntPtr, callback As IntPtr) As Boolean
    End Function
End Class

End Namespace
