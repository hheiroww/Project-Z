Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Physics

Namespace [Shared].Drawing
    Public MustInherit Partial Class Scene
        Public ReadOnly Property PhysicsWorld As New PhysicsWorld()
        Private ReadOnly physicsElements As New List(Of SceneElement)()

        Private Sub SynchronizePhysics()
            physicsElements.Clear()
            PhysicsWorld.Bodies.Clear()
            For Each element In Elements
                If Not element.ParticipatesInPhysics Then Continue For
                element.ReadPhysicsState()
                physicsElements.Add(element)
                PhysicsWorld.Bodies.Add(element.PhysicsBody)
            Next
        End Sub

        Private Sub AdvancePhysics(seconds As Double)
            For Each element In Elements
                If element.IsDisposed Then Continue For
                For Each driver In element.PhysicsDrivers.ToArray()
                    driver.Advance(seconds)
                Next
            Next
            SynchronizePhysics()
            If physicsElements.Count = 0 Then Return
            PhysicsWorld.Advance(seconds)
            For Each element In physicsElements.OrderBy(Function(item) PhysicsDepth(item))
                element.WritePhysicsState()
            Next
        End Sub

        ''' <summary>Projects invalid overlaps immediately without advancing time or adding energy.</summary>
        Public Sub ResolvePhysicsContacts()
            SynchronizePhysics()
            PhysicsWorld.ResolveOverlaps()
            For Each element In physicsElements.OrderBy(Function(item) PhysicsDepth(item))
                element.WritePhysicsState()
            Next
        End Sub

        Friend Sub MovePhysicsElement(element As SceneElement, target As Microsoft.Xna.Framework.Vector3, seconds As Double)
            SynchronizePhysics()
            PhysicsWorld.MoveBody(element.PhysicsBody, target, seconds)
            For Each item In physicsElements.OrderBy(Function(candidate) PhysicsDepth(candidate))
                item.WritePhysicsState()
            Next
        End Sub

        Private Shared Function PhysicsDepth(element As SceneElement) As Integer
            Dim depth = 0
            Dim current = element.RoutedParent
            While current IsNot Nothing
                depth += 1
                current = current.RoutedParent
            End While
            Return depth
        End Function
    End Class
End Namespace
