Imports System.IO
Imports System.IO.MemoryMappedFiles
Imports System.Runtime.InteropServices
Imports System.Text

Namespace RuntimeDiagnostics
' The observer runs outside the UI process so native faults and forced exits
' cannot take the log writer down with them. It observes second-chance faults;
' it never consumes application exceptions or tries to resume a damaged process.
Public NotInheritable Class ApplicationDiagnostics
    Private Shared ReadOnly gate As New Object()
    Public Shared Property SessionPath As String
    Private Shared diagnosticFolder As String
    Private Shared displayName As String
    Private Shared initialized As Integer
    Private Shared ReadOnly recentManaged As New Dictionary(Of UInteger, String)()
    Private Shared managedMap As MemoryMappedFile
    Private Shared managedView As MemoryMappedViewAccessor
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
        ' Initialization is first-call-wins, including an explicit opt-out.
        ' Claim that state before returning so a concurrent/later SceneManager
        ' cannot start an observer after an offscreen host disabled diagnostics.
        If Not options.Enabled Then Return
        Dim entry = Reflection.Assembly.GetEntryAssembly()
        displayName = If(options.ApplicationName, If(entry?.GetName().Name, "Project-Z application"))
        Dim safeName = String.Concat(displayName.Select(Function(ch) If(Path.GetInvalidFileNameChars().Contains(ch), "_"c, ch)))
        diagnosticFolder = If(options.DirectoryPath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Project-Z", safeName, "Diagnostics"))
        Dim silent = Not options.ShowErrorDialogs
        ' Extract-all single-file hosts use an extracted AppContext directory;
        ' the optional observer is a sidecar beside the actual executable.
        Dim processDirectory = Path.GetDirectoryName(Environment.ProcessPath)
        Dim helper = Path.Combine(If(processDirectory, AppContext.BaseDirectory), "ProjectZ.Diagnostics.exe")
        If Not File.Exists(helper) Then helper = Path.Combine(AppContext.BaseDirectory, "ProjectZ.Diagnostics.exe")
        Dim assemblyDirectory = Path.GetDirectoryName(GetType(ApplicationDiagnostics).Assembly.Location)
        If Not File.Exists(helper) AndAlso Not String.IsNullOrEmpty(assemblyDirectory) Then helper = Path.Combine(assemblyDirectory, "ProjectZ.Diagnostics.exe")
        Dim observerInstalled = File.Exists(helper)
        Try
            Directory.CreateDirectory(folder)
            SessionPath = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") & "-" & Environment.ProcessId & ".log")
            If observerInstalled Then
                Try
                    managedMap = MemoryMappedFile.CreateOrOpen(ManagedDiagnosticContext.MapName(SessionPath), ManagedDiagnosticContext.MapCapacity, MemoryMappedFileAccess.ReadWrite)
                    managedView = managedMap.CreateViewAccessor()
                Catch ex As Exception
                    Log("Managed context shared memory unavailable: " & ex.GetType().Name)
                End Try
            End If
            Log("Start; executable=" & Environment.ProcessPath & "; version=" & If(entry?.GetName().Version?.ToString(), "unknown") &
                "; runtime=" & RuntimeInformation.FrameworkDescription & "; OS=" & RuntimeInformation.OSDescription)
            Dim marker = Path.Combine(AppContext.BaseDirectory, "ProjectZ.Release.json")
            If IO.File.Exists(marker) Then Log("Release=" & IO.File.ReadAllText(marker).Trim())
            Dim entryPath = If(String.IsNullOrEmpty(entry?.Location), Environment.ProcessPath, entry.Location)
            If Not String.IsNullOrEmpty(entryPath) AndAlso File.Exists(entryPath) Then Log("Application SHA256=" & Convert.ToHexString(Security.Cryptography.SHA256.HashData(IO.File.ReadAllBytes(entryPath))))
            For Each old In New DirectoryInfo(folder).GetFiles("*.log").OrderByDescending(Function(file) file.LastWriteTimeUtc).Skip(20)
                Try
                    old.Delete()
                    Dim snapshot = old.FullName & ".managed.txt"
                    If File.Exists(snapshot) Then File.Delete(snapshot)
                Catch
                End Try
            Next
            If observerInstalled AndAlso Not ObserverConnection.Register(helper, SessionPath, displayName, silent) Then
                Log("WARNING: Native crash observer did not acknowledge startup.")
            End If
        Catch ex As Exception
            Log("Diagnostic initialization failed: " & ex.ToString())
        End Try
        AddHandler AppDomain.CurrentDomain.UnhandledException, Sub(sender, args) Report("Unhandled; terminating=" & args.IsTerminating, TryCast(args.ExceptionObject, Exception))
        ' A native debugger can see a fatal CLR event before UnhandledException.
        ' Publish bounded context to shared memory; only the background writer
        ' and external observer touch disk for ordinary first-chance snapshots.
        If observerInstalled Then
            AddHandler AppDomain.CurrentDomain.FirstChanceException, Sub(sender, args) CaptureManagedContext(args.Exception)
            managedFlushTimer = New Threading.Timer(Sub(state) FlushManagedContext(), Nothing, 250, 250)
        End If
        AddHandler TaskScheduler.UnobservedTaskException, Sub(sender, args) Report("Unobserved task", args.Exception)
        AddHandler System.Windows.Forms.Application.ThreadException, Sub(sender, args)
                                                                         Report("Windows message callback", args.Exception)
                                                                         Environment.FailFast("Unhandled Windows message callback", args.Exception)
                                                                     End Sub
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
                    Dim at = 16L + slot * ManagedDiagnosticContext.SlotCapacity
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

    Public Shared Sub Log(message As String)
        If String.IsNullOrEmpty(SessionPath) Then Return
        Try
            SyncLock gate
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

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function GetCurrentThreadId() As UInteger
    End Function
End Class

End Namespace
