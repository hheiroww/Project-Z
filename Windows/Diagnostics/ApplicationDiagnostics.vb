Imports System.IO
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
            Log("Start; executable=" & Environment.ProcessPath & "; version=" & If(entry?.GetName().Version?.ToString(), "unknown") &
                "; runtime=" & RuntimeInformation.FrameworkDescription & "; OS=" & RuntimeInformation.OSDescription)
            Dim marker = Path.Combine(AppContext.BaseDirectory, "ProjectZ.Release.json")
            If IO.File.Exists(marker) Then Log("Release=" & IO.File.ReadAllText(marker).Trim())
            Dim executable = If(String.IsNullOrEmpty(entry?.Location), Environment.ProcessPath, entry.Location)
            If Not String.IsNullOrEmpty(executable) Then
                Log("Application SHA256=" & Convert.ToHexString(Security.Cryptography.SHA256.HashData(IO.File.ReadAllBytes(executable))))
            End If
            For Each old In New DirectoryInfo(folder).GetFiles("*.log").OrderByDescending(Function(file) file.LastWriteTimeUtc).Skip(20)
                Try
                    old.Delete()
                    Dim snapshot = old.FullName & ".managed.txt"
                    If File.Exists(snapshot) Then File.Delete(snapshot)
                Catch
                End Try
            Next
            Dim assemblyPath = GetType(ApplicationDiagnostics).Assembly.Location
            Dim helperFolder = If(String.IsNullOrEmpty(assemblyPath), AppContext.BaseDirectory, Path.GetDirectoryName(assemblyPath))
            Dim helper = Path.Combine(helperFolder, "ProjectZ.Diagnostics.exe")
            If Not ObserverConnection.Register(helper, SessionPath, displayName, silent) Then
                Log("WARNING: Native crash observer did not acknowledge startup.")
            End If
        Catch ex As Exception
            Log("Diagnostic initialization failed: " & ex.ToString())
        End Try
        AddHandler AppDomain.CurrentDomain.UnhandledException, Sub(sender, args) Report("Unhandled; terminating=" & args.IsTerminating, TryCast(args.ExceptionObject, Exception))
        ' A native debugger can see the CLR's fatal event before the runtime
        ' calls UnhandledException. Keep bounded, durable exception context so
        ' that the external observer still has a managed stack in that case.
        AddHandler AppDomain.CurrentDomain.FirstChanceException, Sub(sender, args) CaptureManagedContext(args.Exception)
        AddHandler TaskScheduler.UnobservedTaskException, Sub(sender, args) Report("Unobserved task", args.Exception)
        AddHandler System.Windows.Forms.Application.ThreadException, Sub(sender, args)
                                                                         Report("Windows message callback", args.Exception)
                                                                         Environment.FailFast("Unhandled Windows message callback", args.Exception)
                                                                     End Sub
        AddHandler AppDomain.CurrentDomain.ProcessExit, Sub(sender, args) Log("ProcessExit; code=" & Environment.ExitCode)
    End Sub

    Public Shared Sub Report(context As String, failure As Exception)
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
                Dim path = SessionPath & ".managed.txt"
                File.WriteAllText(path & ".new", "Recent managed exception context; these exceptions may have been handled." & Environment.NewLine & String.Join(Environment.NewLine & Environment.NewLine, recentManaged.Values))
                File.Move(path & ".new", path, True)
            End SyncLock
        Catch
        Finally
            capturingManaged = False
        End Try
    End Sub


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

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function GetCurrentThreadId() As UInteger
    End Function
End Class

End Namespace
