Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports System.Threading.Tasks

Namespace [Shared].Composition
    Friend NotInheritable Class DesktopBackdropSetupPrompt
        Implements IDisposable
        Private ReadOnly backdrop As DesktopBackdrop
        Private ReadOnly panel As RectangleElement
        Private ReadOnly message As TextElement
        Private ReadOnly install As Button
        Private ReadOnly plan As DesktopBlurInstallPlan
        Private installation As Task(Of String)
        Friend Sub New(scene As Scene, backdrop As DesktopBackdrop, state As DesktopBackdropState, detail As String)
            Me.backdrop = backdrop
            If OperatingSystem.IsLinux() Then plan = LinuxDesktopBlurSetup.CreatePlan(state = DesktopBackdropState.SessionChangeRequired)
            panel = New RectangleElement(scene) With {.Size = New Vector2(Math.Min(520, scene.InputViewportSize.X - 24), Math.Min(285, scene.InputViewportSize.Y - 24)),
                .HorizontalAlign = HorizontalAlignment.Center, .VerticalAlign = VerticalAlignment.Center, .BackgroundColor = New Color(24, 28, 32), .CornerRadius = 12, .IsOverlay = True}
            message = New TextElement(scene) With {.Text = "Desktop blur" & Environment.NewLine & detail & Environment.NewLine & If(plan Is Nothing, String.Empty, plan.Explanation),
                .Margin = New Thickness(18, 16, 0, 0), .Size = panel.Size - New Vector2(36, 84), .MaxWidth = panel.Size.X - 36, .TextWrapping = TextWrapping.Wrap,
                .ForegroundColor = Color.White, .isMouseBypassEnabled = True, .IsOverlay = True}
            Dim buttonWidth = (panel.Size.X - 48) / 2
            install = New Button(scene) With {.Text = If(plan IsNot Nothing, "Install X11 components", "Allow screen recording"), .AutoSize = ButtonAutoSize.None,
                .Margin = New Thickness(18, CInt(panel.Size.Y - 54), 0, 0), .Size = New Vector2(buttonWidth, 36), .IsOverlay = True,
                .isEnabled = plan IsNot Nothing OrElse (OperatingSystem.IsMacOS() AndAlso state = DesktopBackdropState.PermissionRequired)}
            Dim keep As New Button(scene) With {.Text = "Keep opaque", .AutoSize = ButtonAutoSize.None,
                .Margin = New Thickness(CInt(30 + buttonWidth), CInt(panel.Size.Y - 54), 0, 0), .Size = New Vector2(buttonWidth, 36), .IsOverlay = True}
            AddHandler keep.MouseLeftClick, Sub(point) Dispose()
            AddHandler install.MouseLeftClick, Sub(point)
                If plan IsNot Nothing Then
                    install.isEnabled = False
                    message.Text = "Installing the displayed X11 components. Your operating system may request administrator authentication."
                    installation = LinuxDesktopBlurSetup.InstallAsync(plan, confirmedByUser:=True)
                Else
                    Try
                        If backdrop.RequestCapturePermission() Then
                            backdrop.Retry()
                            Dispose()
                        Else
                            message.Text = "Screen recording permission was not granted. The opaque fallback remains. You can enable access in macOS System Settings and retry desktop blur."
                        End If
                    Catch ex As Exception
                        message.Text = ex.Message
                    End Try
                End If
            End Sub
            panel.Children.AddRange({message, install, keep})
            scene.AddElement(panel)
        End Sub
        Friend Sub Tick()
            If installation Is Nothing OrElse Not installation.IsCompleted Then Return
            Try
                message.Text = installation.GetAwaiter().GetResult()
                If Not plan.SessionChangeRequired Then backdrop.Retry()
            Catch ex As Exception
                message.Text = ex.Message
                install.isEnabled = True
            Finally
                installation = Nothing
            End Try
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            panel.Dispose()
        End Sub
    End Class
End Namespace
