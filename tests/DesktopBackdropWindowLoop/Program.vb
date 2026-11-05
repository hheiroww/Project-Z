Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Module Program
    Private checks As Integer
    Private Sub Require(value As Boolean, message As String)
        If Not value Then Throw New InvalidOperationException(message)
        checks += 1
    End Sub

    ' Exercise only this fixture's HWND. It is offscreen and shown without
    ' activation; no user windows, input, pixels or desktop capture are involved.
    <STAThread>
    Sub Main()
        Using host As New Form With {.ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual,
                                    .Location = New System.Drawing.Point(-30000, -30000),
                                    .Size = New System.Drawing.Size(240, 160)}
            Dim hwnd = host.Handle
            Dim count = 0, enabled = True
            Dim timer = New IntPtr(&H505A424C)
            Dim draw As Action = Sub()
                                     count += 1
                                     SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                                 End Sub
            Using loopHost As New ProjectZ.Windows.Composition.DesktopBackdropWindowLoop(hwnd, Function() enabled, draw)
                ShowWindow(hwnd, 4) ' SW_SHOWNOACTIVATE
                SendMessage(hwnd, &H231, IntPtr.Zero, IntPtr.Zero)
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = 1, "A modal frame must render once and suppress a reentrant timer.")
                Dim firstCount = count
                enabled = False
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = firstCount, "Disabled material must not draw.")
                enabled = True
                ShowWindow(hwnd, 0)
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = firstCount, "Hidden material must not draw.")
                ShowWindow(hwnd, 4)
                SendMessage(hwnd, &H232, IntPtr.Zero, IntPtr.Zero)
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = firstCount, "Exiting the modal loop must stop its timer.")
                enabled = False
                SendMessage(hwnd, &H231, IntPtr.Zero, IntPtr.Zero)
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = firstCount, "Entering while temporarily busy must not render a frame.")
                enabled = True
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = firstCount + 1, "A transient entry guard must recover during the same modal loop.")
                firstCount = count
                SendMessage(hwnd, &H232, IntPtr.Zero, IntPtr.Zero)
                SendMessage(hwnd, &H231, IntPtr.Zero, IntPtr.Zero)
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = firstCount + 1, "A later modal loop must restart its frame timer.")
                firstCount = count
                loopHost.Dispose()
                SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                Require(count = firstCount, "Disposal must stop the timer and release the HWND hook.")
                ' Only the backend's actual Game.Window may reuse its existing
                ' pump. This separate fixture HWND defaults to its own pump,
                ' including when the consuming game uses WindowsDX.
                Using existingPump As New ProjectZ.Windows.Composition.DesktopBackdropWindowLoop(hwnd, Function() enabled, draw, useExistingFramePump:=True)
                    SendMessage(hwnd, &H231, IntPtr.Zero, IntPtr.Zero)
                    SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                    Require(count = firstCount, "An existing frame pump must not acquire a duplicate timer.")
                    SendMessage(hwnd, &H232, IntPtr.Zero, IntPtr.Zero)
                    SendMessage(hwnd, &H231, IntPtr.Zero, IntPtr.Zero)
                    SendMessage(hwnd, &H113, timer, IntPtr.Zero)
                    Require(count = firstCount, "An existing frame pump must remain unduplicated on reentry.")
                End Using
                ShowWindow(hwnd, 0)
            End Using
        End Using
        CheckSharedFrameOwner()
        Console.WriteLine($"DESKTOP_BACKDROP_WINDOW_LOOP_OK checks={checks}")
    End Sub

    Private Sub CheckSharedFrameOwner()
        Using first As New Form With {.ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual,
                                     .Location = New System.Drawing.Point(-30000, -30000)},
              second As New Form With {.ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual,
                                      .Location = New System.Drawing.Point(-30000, -30000)}
            Dim firstHandle = first.Handle, secondHandle = second.Handle
            Dim owner As New Object()
            Dim firstFrames = 0, secondFrames = 0
            Dim timer = New IntPtr(&H505A424C)
            Dim drawFirst As Action = Sub()
                                          firstFrames += 1
                                          SendMessage(secondHandle, &H113, timer, IntPtr.Zero)
                                      End Sub
            Dim drawSecond As Action = Sub()
                                           secondFrames += 1
                                           SendMessage(firstHandle, &H113, timer, IntPtr.Zero)
                                       End Sub
            Using firstLoop As New ProjectZ.Windows.Composition.DesktopBackdropWindowLoop(firstHandle, Function() True, drawFirst, frameOwner:=owner),
                  secondLoop As New ProjectZ.Windows.Composition.DesktopBackdropWindowLoop(secondHandle, Function() True, drawSecond, frameOwner:=owner)
                ShowWindow(firstHandle, 4)
                ShowWindow(secondHandle, 4)
                SendMessage(firstHandle, &H231, IntPtr.Zero, IntPtr.Zero)
                SendMessage(secondHandle, &H231, IntPtr.Zero, IntPtr.Zero)
                SendMessage(firstHandle, &H113, timer, IntPtr.Zero)
                Require(firstFrames = 1 AndAlso secondFrames = 0, "A modal frame must suppress another HWND reentering the same frame owner.")
                SendMessage(secondHandle, &H113, timer, IntPtr.Zero)
                Require(firstFrames = 1 AndAlso secondFrames = 1, "The shared gate must release and protect reentry from either HWND.")
                SendMessage(firstHandle, &H113, timer, IntPtr.Zero)
                Require(firstFrames = 2 AndAlso secondFrames = 1, "A completed modal dispatch must leave the shared frame owner available.")
                SendMessage(firstHandle, &H232, IntPtr.Zero, IntPtr.Zero)
                SendMessage(secondHandle, &H232, IntPtr.Zero, IntPtr.Zero)
                ShowWindow(firstHandle, 0)
                ShowWindow(secondHandle, 0)
            End Using
        End Using
    End Sub

    <DllImport("user32.dll", ExactSpelling:=True, CharSet:=CharSet.Unicode, EntryPoint:="SendMessageW")>
    Private Function SendMessage(hwnd As IntPtr, msg As Integer, wparam As IntPtr, lparam As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll", ExactSpelling:=True)>
    Private Function ShowWindow(hwnd As IntPtr, command As Integer) As Boolean
    End Function
End Module
