Imports System.Diagnostics
Imports System.IO
Imports System.IO.MemoryMappedFiles
Imports System.Runtime.InteropServices
Imports System.Text

Namespace RuntimeDiagnostics
    ' Each target has its own Windows debugger thread and diagnostic log.
    Friend NotInheritable Class NativeObserver
        Private Shared ReadOnly dumpGate As New Object()
        Private ReadOnly target As Process
        Private ReadOnly session As String
        Private ReadOnly observerLog As String
        Private ReadOnly folder As String
        Private ReadOnly applicationName As String
        Private ReadOnly silent As Boolean

        Friend Sub New(target As Process, session As String, applicationName As String, silent As Boolean)
            Me.target = target
            Me.session = session
            Me.observerLog = Path.ChangeExtension(session, ".observer.log")
            Me.folder = Path.GetDirectoryName(session)
            Me.applicationName = applicationName
            Me.silent = silent
        End Sub

        Friend Sub Watch(onReady As Action)
            Dim targetId = target.Id
            Dim nativeFault = False
            Dim attached = False
            Dim events = IntPtr.Zero
            Try
                events = Marshal.AllocHGlobal(176) ' DEBUG_EVENT uses the same layout on x64 and Arm64.
                attached = DebugActiveProcess(CUInt(targetId))
                If attached Then
                    DebugSetProcessKillOnExit(False)
                    Log("Native observer attached; target=" & targetId)
                    Dim notifyReady = onReady
                    onReady = Nothing
                    notifyReady()
                    ObserveEvents(events, nativeFault)
                Else
                    Log("Native observer attach failed; Win32=" & Marshal.GetLastWin32Error())
                End If
            Catch ex As Exception
                Report("Native observer failed", ex)
            Finally
                If attached Then DebugActiveProcessStop(CUInt(targetId))
                If events <> IntPtr.Zero Then Marshal.FreeHGlobal(events)
                onReady?.Invoke()
            End Try

            ' Even when another debugger owns the target, its registration stays live
            ' until the application exits.
            Try
                target.WaitForExit()
                If Not attached Then Log("Observed exit without debugger; code=" & target.ExitCode.ToString("X8"))
            Catch ex As Exception
                Report("Waiting for application exit failed", ex)
            End Try
            If nativeFault AndAlso Not silent Then ShowCrashNotification()
        End Sub

        Private Sub ObserveEvents(events As IntPtr, ByRef nativeFault As Boolean)
            Dim targetId = target.Id
            Dim graphicsOriginCaptured = False
            Dim attachBreakpoint = True
            Do
                If Not WaitForDebugEvent(events, 1000UI) Then
                    If target.HasExited Then Exit Do
                    Continue Do
                End If
                Dim kind = Marshal.ReadInt32(events, 0)
                Dim processId = CUInt(Marshal.ReadInt32(events, 4))
                Dim threadId = CUInt(Marshal.ReadInt32(events, 8))
                Dim status As UInteger = &H10002UI ' DBG_CONTINUE for lifecycle events
                Try
                    If kind = 1 Then
                        Dim code = Marshal.ReadInt32(events, 16)
                        Dim firstChance = Marshal.ReadInt32(events, 168) <> 0
                        ' Let application/runtime handlers see every exception,
                        ' except the debugger's initial attach breakpoint.
                        If code = &H80000003 AndAlso attachBreakpoint Then
                            attachBreakpoint = False
                        Else
                            status = &H80010001UI
                        End If
                        If firstChance AndAlso code = &HE06D7363 Then
                            Dim graphicsError = ReadGraphicsException(target, events)
                            If graphicsError IsNot Nothing Then
                                Log("Native graphics exception before cleanup; OS thread=" & threadId & "; " & graphicsError)
                                If Not graphicsOriginCaptured Then
                                    graphicsOriginCaptured = True
                                    WriteDump(Path.ChangeExtension(session, ".native-origin.log"), events, threadId)
                                End If
                            End If
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
                            Try
                                For Each loaded As ProcessModule In target.Modules
                                    Dim start = loaded.BaseAddress.ToInt64()
                                    If address >= start AndAlso address - start < loaded.ModuleMemorySize Then
                                        Log("Fault module=" & loaded.FileName & "; offset=0x" & (address - start).ToString("X"))
                                        Exit For
                                    End If
                                Next
                            Catch ex As ComponentModel.Win32Exception
                                Log("Fault module unavailable; Win32=" & ex.NativeErrorCode)
                            End Try
                            WriteDump(session, events, threadId)
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
                Finally
                    If Not ContinueDebugEvent(processId, threadId, status) Then
                        Throw New ComponentModel.Win32Exception(Marshal.GetLastWin32Error())
                    End If
                End Try
                If kind = 5 Then Exit Do
            Loop
        End Sub

        Private Shared Function ReadManagedMap(session As String) As String
            Try
                Using map = MemoryMappedFile.OpenExisting(ManagedDiagnosticContext.MapName(session), MemoryMappedFileRights.Read), view = map.CreateViewAccessor(0, ManagedDiagnosticContext.MapCapacity, MemoryMappedFileAccess.Read)
                    Dim slot = view.ReadInt32(0)
                    If slot < 0 OrElse slot > 1 Then Return String.Empty
                    Dim at = 16L + slot * ManagedDiagnosticContext.SlotCapacity
                    Dim length = view.ReadInt32(at)
                    If length <= 0 OrElse length > ManagedDiagnosticContext.SlotCapacity - 4 Then Return String.Empty
                    Dim bytes(length - 1) As Byte
                    view.ReadArray(at + 4, bytes, 0, bytes.Length)
                    Return Encoding.UTF8.GetString(bytes)
                End Using
            Catch
                Return String.Empty
            End Try
        End Function

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

        Private Sub WriteDump(session As String, events As IntPtr, threadId As UInteger)
            ' DbgHelp is single-threaded. Also serialize retention in the shared log folder.
            SyncLock dumpGate
                Dim arm64 = RuntimeInformation.ProcessArchitecture = Architecture.Arm64
                Dim matchingArchitecture = CanReadThreadContext(arm64)
                Dim contextSize = If(arm64, &H390, 1232)
                Dim contextBuffer = Marshal.AllocHGlobal(contextSize + 15)
                Dim context = New IntPtr((contextBuffer.ToInt64() + 15L) And Not 15L)
                Dim pointers = Marshal.AllocHGlobal(16)
                Dim exceptionInfo = Marshal.AllocHGlobal(16) ' MINIDUMP headers use pack(4)
                Dim thread = If(matchingArchitecture, OpenThread(&H48UI, False, threadId), IntPtr.Zero)
                Try
                    For Each old In New DirectoryInfo(folder).GetFiles("*.dmp").OrderByDescending(Function(file) file.LastWriteTimeUtc).Skip(2)
                        old.Delete()
                    Next
                    Using output As New FileStream(Path.ChangeExtension(session, ".dmp"), FileMode.Create, FileAccess.Write, FileShare.Read)
                        ' Include thread stacks, module metadata and unloaded modules.
                        ' Full process memory (chat contents/credentials) is not requested.
                        Marshal.Copy(New Byte(contextSize - 1) {}, 0, context, contextSize)
                        If arm64 Then
                            Marshal.WriteInt32(context, 0, &H400007) ' ARM64 CONTEXT_FULL
                        Else
                            Marshal.WriteInt32(context, 48, &H10001F) ' x64 CONTEXT_ALL
                        End If
                        Dim info = IntPtr.Zero
                        If thread <> IntPtr.Zero AndAlso GetThreadContext(thread, context) Then
                            Marshal.WriteIntPtr(pointers, 0, IntPtr.Add(events, 16)) ' local EXCEPTION_RECORD64
                            Marshal.WriteIntPtr(pointers, 8, context)
                            Marshal.WriteInt32(exceptionInfo, 0, CInt(threadId))
                            Marshal.WriteIntPtr(exceptionInfo, 4, pointers)
                            Marshal.WriteInt32(exceptionInfo, 12, 0) ' pointers belong to observer
                            info = exceptionInfo
                        ElseIf matchingArchitecture Then
                            Log("Exception context unavailable; Win32=" & Marshal.GetLastWin32Error())
                        Else
                            Log("Exception context unavailable; target architecture is incompatible or could not be determined")
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
            End SyncLock
        End Sub

        Private Function CanReadThreadContext(arm64 As Boolean) As Boolean
            Const arm64Machine As UShort = &HAA64US
            Const x64Machine As UShort = &H8664US
            Dim observerMachine = If(arm64, arm64Machine, x64Machine)
            If OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
                Const processMachineTypeInfo As Integer = 9
                Dim information As ProcessMachineInformation
                Return GetProcessInformation(target.Handle, processMachineTypeInfo, information,
                                             CUInt(Marshal.SizeOf(Of ProcessMachineInformation)())) AndAlso information.Machine = observerMachine
            End If
            If OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299) Then
                Dim targetMachine As UShort
                Dim nativeMachine As UShort
                Return IsWow64Process2(target.Handle, targetMachine, nativeMachine) AndAlso
                       If(targetMachine = 0US, nativeMachine, targetMachine) = observerMachine
            End If
            Dim isWow64 As Boolean
            Return Not arm64 AndAlso IsWow64Process(target.Handle, isWow64) AndAlso Not isWow64
        End Function

        Private Sub Log(message As String)
            Try
                Using stream As New FileStream(observerLog, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), writer As New StreamWriter(stream, New UTF8Encoding(False))
                    writer.WriteLine(DateTime.UtcNow.ToString("O") & " | pid=" & Environment.ProcessId & " thread=" & Environment.CurrentManagedThreadId & " | " & message)
                    writer.Flush()
                    stream.Flush(True)
                End Using
            Catch
                ' Logging must not interrupt debugging or monitoring the application.
            End Try
        End Sub

        Private Sub Report(context As String, failure As Exception)
            Log(context & Environment.NewLine & failure.ToString())
        End Sub

        Private Sub ShowCrashNotification()
            Const currentSession As UInteger = &HFFFFFFFFUI
            Const errorIcon As UInteger = &H10UI
            Dim title = applicationName & " error"
            Dim message = applicationName & " closed because of a native error. Diagnostic logs and a crash dump were saved in:" & Environment.NewLine & folder
            Dim response As UInteger
            ' Windows owns the message box, allowing the observer to exit without waiting.
            If Not WTSSendMessage(IntPtr.Zero, currentSession, title, CUInt(Encoding.Unicode.GetByteCount(title)),
                                  message, CUInt(Encoding.Unicode.GetByteCount(message)), errorIcon, 120UI, response, False) Then
                Log("Native crash notification failed; Win32=" & Marshal.GetLastWin32Error())
            End If
        End Sub

        <DllImport("wtsapi32.dll", EntryPoint:="WTSSendMessageW", CharSet:=CharSet.Unicode, ExactSpelling:=True, SetLastError:=True)>
        Private Shared Function WTSSendMessage(server As IntPtr, sessionId As UInteger, title As String, titleLength As UInteger,
                                               message As String, messageLength As UInteger, style As UInteger, timeout As UInteger,
                                               ByRef response As UInteger, wait As Boolean) As Boolean
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
        <StructLayout(LayoutKind.Sequential)>
        Private Structure ProcessMachineInformation
            Public Machine As UShort
            Public Reserved As UShort
            Public Attributes As UInteger
        End Structure
        <DllImport("kernel32.dll")>
        Private Shared Function GetProcessInformation(process As IntPtr, informationClass As Integer,
                                                      ByRef information As ProcessMachineInformation, size As UInteger) As Boolean
        End Function
        <DllImport("kernel32.dll")>
        Private Shared Function IsWow64Process2(process As IntPtr, ByRef processMachine As UShort, ByRef nativeMachine As UShort) As Boolean
        End Function
        <DllImport("kernel32.dll")>
        Private Shared Function IsWow64Process(process As IntPtr, ByRef isWow64 As Boolean) As Boolean
        End Function
        <DllImport("dbghelp.dll", SetLastError:=True)>
        Private Shared Function MiniDumpWriteDump(process As IntPtr, processId As UInteger, file As IntPtr, dumpType As UInteger, exception As IntPtr, userStream As IntPtr, callback As IntPtr) As Boolean
        End Function
    End Class
End Namespace
