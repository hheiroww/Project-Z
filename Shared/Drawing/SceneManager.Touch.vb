Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Input.Touch

Namespace [Shared].Drawing
    Public Partial Class SceneManager
        Private touchPointer As Integer?
        Private touchOrigin As Point
        Private touchLast As Point
        Private touchScene As Scene
        Private touchDragged As Boolean
        Private touchPollingAvailable As Boolean = True

        ''' <summary>Inject a touch contact using client coordinates. One primary contact owns a gesture.</summary>
        Public Sub InjectTouchDown(id As Integer, x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If touchScene IsNot Nothing AndAlso touchScene IsNot ActiveScene Then CancelTouchInput()
            If ActiveScene Is Nothing OrElse touchPointer.HasValue Then Return
            touchPointer = id
            touchScene = ActiveScene
            touchOrigin = ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight)
            touchLast = touchOrigin
            touchDragged = False
            touchScene.MouseLeftDown(touchOrigin)
        End Sub

        Public Sub InjectTouchMove(id As Integer, x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If Not touchPointer.HasValue OrElse touchPointer.Value <> id Then Return
            If ActiveScene IsNot touchScene Then
                CancelTouchInput()
                Return
            End If
            Dim point = ScaleExternalMousePosition(New Point(x, y), clientWidth, clientHeight)
            If point = touchLast Then Return
            touchScene.MouseMove(point, touchLast)
            Dim delta = point - touchOrigin
            If touchDragged OrElse Math.Abs(delta.X) > DragThreshold OrElse Math.Abs(delta.Y) > DragThreshold Then
                touchDragged = True
                touchScene.MouseDrag(point, touchOrigin)
            End If
            touchLast = point
        End Sub

        Public Sub InjectTouchUp(id As Integer, x As Integer, y As Integer, clientWidth As Integer, clientHeight As Integer)
            If Not touchPointer.HasValue OrElse touchPointer.Value <> id Then Return
            InjectTouchMove(id, x, y, clientWidth, clientHeight)
            If Not touchPointer.HasValue Then Return
            Dim scene = touchScene
            Dim point = touchLast
            touchPointer = Nothing
            touchScene = Nothing
            scene.MouseLeftUp(point)
            If touchDragged Then
                scene.MouseDragDrop(point, touchOrigin)
            Else
                scene.MouseLeftClick(point)
            End If
        End Sub

        Public Sub CancelTouchInput()
            Dim scene = touchScene
            touchPointer = Nothing
            touchScene = Nothing
            touchDragged = False
            scene?.CancelPointerInput()
        End Sub

        Private Function ProcessTouchInput() As Boolean
            If Not touchPollingAvailable Then Return False
            Dim contacts As TouchCollection
            Try
                contacts = TouchPanel.GetState()
            Catch ex As NotSupportedException
                touchPollingAvailable = False
                Return False
            End Try
            Dim handled = touchPointer.HasValue
            Dim found = False
            Dim bounds = Sender.Window.ClientBounds
            For Each contact In contacts
                Dim x = CInt(contact.Position.X), y = CInt(contact.Position.Y)
                If touchPointer.HasValue AndAlso touchPointer.Value = contact.Id Then found = True
                Select Case contact.State
                    Case TouchLocationState.Pressed
                        InjectTouchDown(contact.Id, x, y, bounds.Width, bounds.Height)
                        If touchPointer.HasValue AndAlso touchPointer.Value = contact.Id Then found = True
                    Case TouchLocationState.Moved
                        InjectTouchMove(contact.Id, x, y, bounds.Width, bounds.Height)
                    Case TouchLocationState.Released
                        InjectTouchUp(contact.Id, x, y, bounds.Width, bounds.Height)
                End Select
                handled = handled OrElse touchPointer.HasValue OrElse contact.State = TouchLocationState.Released
            Next
            If touchPointer.HasValue AndAlso Not found Then CancelTouchInput()
            Return handled
        End Function
    End Class
End Namespace
