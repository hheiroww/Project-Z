Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Input

Namespace [Shared].Drawing.UI
    ''' <summary>Builds scene content in small batches between input and rendering.</summary>
    ''' <remarks>
    ''' Create and use this object on the scene thread. Start a new request to cancel the
    ''' previous request for the same panel. Keep each step small because a running step
    ''' finishes before the next frame or request. Prepare data that does not touch the
    ''' scene or graphics resources on a worker before queuing its UI steps.
    ''' </remarks>
    Public NotInheritable Class SceneLoadingWork
        Implements IDisposable

        Private Shared ReadOnly FrameBudget As TimeSpan = TimeSpan.FromMilliseconds(3)
        Private Shared ReadOnly IndicatorDelay As TimeSpan = TimeSpan.FromMilliseconds(120)

        Private ReadOnly scene As Scene
        Private ReadOnly indicator As LoadingIndicator
        Private ReadOnly threadId As Integer = Environment.CurrentManagedThreadId
        Private current As LoadRequest
        Private processing As LoadRequest
        Private disposed As Boolean

        Public Sub New(scene As Scene, Optional loadingIndicator As LoadingIndicator = Nothing)
            ArgumentNullException.ThrowIfNull(scene)
            ObjectDisposedException.ThrowIf(scene.IsDisposed, scene)
            If loadingIndicator IsNot Nothing AndAlso loadingIndicator.OwnerScene IsNot scene Then
                Throw New ArgumentException("The loading indicator must belong to this scene.", NameOf(loadingIndicator))
            End If
            Me.scene = scene
            indicator = loadingIndicator
            SetIndicator(False)
            AddHandler scene.FrameUpdated, AddressOf ProcessFrame
            AddHandler scene.Disposed, AddressOf Dispose
        End Sub

        Public ReadOnly Property IsLoading As Boolean
            Get
                Return current IsNot Nothing
            End Get
        End Property

        ''' <summary>Runs scene-thread steps across frames and returns when the request finishes.</summary>
        ''' <remarks>Include any scene updates required on completion in the last step.</remarks>
        Public Function RunAsync(steps As IEnumerable(Of Action), Optional cancellationToken As CancellationToken = Nothing) As Task
            CheckAccess()
            ObjectDisposedException.ThrowIf(disposed, Me)
            ArgumentNullException.ThrowIfNull(steps)
            If cancellationToken.IsCancellationRequested Then
                Cancel()
                Return Task.FromCanceled(cancellationToken)
            End If

            Dim previous = current
            Dim request As New LoadRequest(steps, cancellationToken)
            current = request
            If previous IsNot Nothing Then Finish(previous, cancelled:=True)
            scene.RequestRender()
            Return request.Completion.Task
        End Function

        Public Sub Cancel()
            CheckAccess()
            If current IsNot Nothing Then Finish(current, cancelled:=True)
        End Sub

        Private Sub ProcessFrame(gameTime As GameTime)
            If current Is Nothing OrElse processing IsNot Nothing Then Return
            Dim request = current
            If request.Token.IsCancellationRequested Then
                Finish(request, cancelled:=True)
                Return
            End If

            Dim frameStarted = Stopwatch.GetTimestamp()
            Dim complete As Boolean
            Dim failure As Exception = Nothing
            processing = request
            Try
                If Stopwatch.GetElapsedTime(request.Started, frameStarted) >= IndicatorDelay Then SetIndicator(True)
                If request.Enumerator Is Nothing AndAlso current Is request Then request.Enumerator = request.Steps.GetEnumerator()
                While current Is request AndAlso Not request.Token.IsCancellationRequested
                    If Not request.Enumerator.MoveNext() Then
                        complete = True
                        Exit While
                    End If
                    If current IsNot request OrElse request.Token.IsCancellationRequested Then Exit While
                    request.Enumerator.Current.Invoke()
                    If Stopwatch.GetElapsedTime(frameStarted) >= FrameBudget Then Exit While
                End While
            Catch ex As Exception
                failure = ex
            Finally
                processing = Nothing
            End Try

            If current IsNot request Then
                ' A step can request different content. Release the old iterator once the step returns.
                ReleaseIterator(request, failure)
            ElseIf complete OrElse failure IsNot Nothing OrElse request.Token.IsCancellationRequested Then
                Finish(request, cancelled:=request.Token.IsCancellationRequested, failure:=failure)
            Else
                scene.RequestRender()
            End If
        End Sub

        Private Sub Finish(request As LoadRequest, cancelled As Boolean, Optional failure As Exception = Nothing)
            If current Is request Then
                current = Nothing
                SetIndicator(False)
            End If
            If processing IsNot request Then ReleaseIterator(request, failure)

            If cancelled Then
                request.Completion.TrySetCanceled(request.Token)
            ElseIf failure IsNot Nothing Then
                request.Completion.TrySetException(failure)
            Else
                request.Completion.TrySetResult()
            End If
        End Sub

        Private Shared Sub ReleaseIterator(request As LoadRequest, ByRef failure As Exception)
            Try
                request.Enumerator?.Dispose()
            Catch ex As Exception
                failure = If(failure, ex)
            Finally
                request.Enumerator = Nothing
            End Try
        End Sub

        Private Sub SetIndicator(value As Boolean)
            If indicator IsNot Nothing AndAlso Not indicator.IsDisposed Then indicator.IsLoading = value
        End Sub

        Private Sub CheckAccess()
            If Environment.CurrentManagedThreadId <> threadId Then
                Throw New InvalidOperationException("Scene loading work must be used on the scene thread.")
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            CheckAccess()
            disposed = True
            RemoveHandler scene.FrameUpdated, AddressOf ProcessFrame
            RemoveHandler scene.Disposed, AddressOf Dispose
            Cancel()
        End Sub

        Private NotInheritable Class LoadRequest
            Public ReadOnly Steps As IEnumerable(Of Action)
            Public ReadOnly Token As CancellationToken
            Public ReadOnly Started As Long = Stopwatch.GetTimestamp()
            Public ReadOnly Completion As New TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            Public Enumerator As IEnumerator(Of Action)

            Public Sub New(steps As IEnumerable(Of Action), token As CancellationToken)
                Me.Steps = steps
                Me.Token = token
            End Sub
        End Class
    End Class
End Namespace
