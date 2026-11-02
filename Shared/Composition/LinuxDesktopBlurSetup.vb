Imports System.Diagnostics
Imports System.IO
Imports System.Threading.Tasks

Namespace [Shared].Composition
    Public NotInheritable Class DesktopBlurInstallPlan
        Public ReadOnly Property PackageManager As String
        Public ReadOnly Property Arguments As IReadOnlyList(Of String)
        Public ReadOnly Property Packages As IReadOnlyList(Of String)
        Public ReadOnly Property SessionChangeRequired As Boolean
        Public ReadOnly Property Explanation As String
        Friend Sub New(manager As String, arguments As IEnumerable(Of String), packages As IEnumerable(Of String), needsSession As Boolean)
            PackageManager = manager
            Me.Arguments = Array.AsReadOnly(arguments.Concat(packages).ToArray())
            Me.Packages = Array.AsReadOnly(packages.ToArray())
            SessionChangeRequired = needsSession
            Explanation = "Install " & String.Join(", ", Me.Packages) & "." &
                If(needsSession, " This installs X11 components. Select a supported X11 desktop session at the next login; the current Wayland session keeps the opaque fallback.", " The application will retry desktop blur after installation.")
        End Sub
    End Class

    ''' <summary>Runs the distribution package manager only after an explicit application-user action.
    ''' Authentication belongs to the OS polkit agent. No passwords are collected by Project-Z.</summary>
    Public Module LinuxDesktopBlurSetup
        Public Function CreatePlan(Optional includeXorgSession As Boolean = False) As DesktopBlurInstallPlan
            If Not OperatingSystem.IsLinux() OrElse Not File.Exists("/etc/os-release") Then Return Nothing
            Dim values As New Dictionary(Of String, String)(StringComparer.Ordinal)
            For Each line In File.ReadLines("/etc/os-release")
                Dim separator = line.IndexOf("="c)
                If separator > 0 Then values(line.Substring(0, separator)) = line.Substring(separator + 1).Trim(""""c, "'"c)
            Next
            Dim id As String = Nothing
            values.TryGetValue("ID", id)
            Return PlanForDistribution(id, includeXorgSession)
        End Function
        Public Function PlanForDistribution(distribution As String, Optional includeXorgSession As Boolean = False) As DesktopBlurInstallPlan
            Dim packages As New List(Of String)
            Select Case If(distribution, String.Empty).ToLowerInvariant()
                Case "ubuntu", "debian", "linuxmint", "pop"
                    packages.AddRange({"libx11-6", "libxcomposite1", "libxrender1", "libxext6"})
                    If includeXorgSession Then packages.AddRange({"xserver-xorg-core", "xinit"})
                    Return New DesktopBlurInstallPlan("/usr/bin/apt-get", {"--yes", "install", "--no-install-recommends"}, packages, includeXorgSession)
                Case "arch", "manjaro", "endeavouros"
                    packages.AddRange({"libx11", "libxcomposite", "libxrender", "libxext"})
                    If includeXorgSession Then packages.AddRange({"xorg-server", "xorg-xinit"})
                    Return New DesktopBlurInstallPlan("/usr/bin/pacman", {"-S", "--needed", "--noconfirm"}, packages, includeXorgSession)
                Case "fedora"
                    packages.AddRange({"libX11", "libXcomposite", "libXrender", "libXext"})
                    If includeXorgSession Then packages.AddRange({"xorg-x11-server-Xorg", "xorg-x11-xinit"})
                    Return New DesktopBlurInstallPlan("/usr/bin/dnf", {"--assumeyes", "install"}, packages, includeXorgSession)
                Case Else
                    Return Nothing
            End Select
        End Function
        Public Async Function InstallAsync(plan As DesktopBlurInstallPlan, confirmedByUser As Boolean) As Task(Of String)
            ArgumentNullException.ThrowIfNull(plan)
            If Not confirmedByUser Then Throw New InvalidOperationException("The user must approve the displayed package installation plan.")
            If Not OperatingSystem.IsLinux() Then Throw New PlatformNotSupportedException("X11 dependencies are installed on Linux only.")
            If Not File.Exists("/usr/bin/pkexec") OrElse Not File.Exists(plan.PackageManager) Then Throw New InvalidOperationException("The system package manager or polkit authentication agent is unavailable. Install the listed packages using your distribution's package manager.")
            Dim start As New ProcessStartInfo("/usr/bin/pkexec") With {.UseShellExecute = False, .RedirectStandardOutput = True, .RedirectStandardError = True, .CreateNoWindow = True}
            start.ArgumentList.Add(plan.PackageManager)
            For Each argument In plan.Arguments
                start.ArgumentList.Add(argument)
            Next
            Dim lines As New System.Collections.Concurrent.ConcurrentQueue(Of String)
            Dim remember As Action(Of Object, DataReceivedEventArgs) = Sub(sender, args)
                If args.Data Is Nothing Then Return
                lines.Enqueue(args.Data.Substring(0, Math.Min(256, args.Data.Length)))
                While lines.Count > 48
                    Dim ignored As String = Nothing
                    lines.TryDequeue(ignored)
                End While
            End Sub
            Using process As New Process With {.StartInfo = start}
                AddHandler process.OutputDataReceived, Sub(sender, args) remember(sender, args)
                AddHandler process.ErrorDataReceived, Sub(sender, args) remember(sender, args)
                If Not process.Start() Then Throw New InvalidOperationException("The package manager could not start.")
                process.BeginOutputReadLine() : process.BeginErrorReadLine()
                Await process.WaitForExitAsync().ConfigureAwait(False)
                If process.ExitCode <> 0 Then Throw New InvalidOperationException("Package installation was cancelled or failed." & Environment.NewLine & String.Join(Environment.NewLine, lines))
            End Using
            Return If(plan.SessionChangeRequired, "X11 components installed. Select a supported X11 desktop session at the next login; this Wayland session keeps the opaque fallback.", "Desktop blur dependencies installed.")
        End Function
    End Module
End Namespace
