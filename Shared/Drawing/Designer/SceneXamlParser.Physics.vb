Imports System.Xml
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports ProjectZ.Shared.Physics

Namespace [Shared].Drawing.Designer
    Public Partial Class SceneXamlParser
        Private Sub RegisterPhysicsFactories()
            _elementFactories("Surface3D") = Function(xml) CreatePhysicsSurface(xml)
            _elementFactories("Surface3DElement") = Function(xml) CreatePhysicsSurface(xml)
            _elementFactories("ParticleEmitter") = Function(xml) CreateParticleEmitter(xml)
            _elementFactories("ParticleEmitterElement") = Function(xml) CreateParticleEmitter(xml)
        End Sub

        Private Function CreatePhysicsSurface(xml As XmlElement) As SceneElement
            Dim surface As New Surface3DElement(_scene) With {
                .Position3D = PhysicsVector3(xml, "Position3D", Vector3.Zero),
                .PhysicsSize3D = PhysicsVector3(xml, "PhysicsSize3D", Vector3.One),
                .Velocity3D = PhysicsVector3(xml, "Velocity3D", Vector3.Zero),
                .Gravity3D = PhysicsVector3(xml, "Gravity3D", New Vector3(0, -9.81F, 0))}
            Return surface
        End Function

        Private Function CreateParticleEmitter(xml As XmlElement) As SceneElement
            Dim emitter As New ParticleEmitterElement(_scene, Math.Clamp(GetAttributeInteger(xml, "Capacity", 4096), 1, 16000), GetAttributeInteger(xml, "Seed", 1))
            emitter.Is3D = GetAttributeBoolean(xml, "Is3D", False)
            emitter.Position3D = PhysicsVector3(xml, "Position3D", Vector3.Zero)
            emitter.Simulation.EmissionRate = GetAttributeDouble(xml, "EmissionRate", 30)
            emitter.Simulation.ParticleLifetime = GetAttributeDouble(xml, "ParticleLifetime", 2)
            emitter.Simulation.ParticleSize = PhysicsVector2(xml, "ParticleSize", New Vector2(8))
            emitter.Simulation.InitialVelocity = PhysicsVector3(xml, "InitialVelocity", New Vector3(0, -50, 0))
            emitter.Simulation.VelocitySpread = PhysicsVector3(xml, "VelocitySpread", New Vector3(20, 20, 0))
            emitter.Simulation.PhysicsEnabled = GetAttributeBoolean(xml, "ParticlePhysicsEnabled", True)
            emitter.Simulation.GravityEnabled = GetAttributeBoolean(xml, "ParticleGravityEnabled", False)
            emitter.Simulation.Gravity = PhysicsVector3(xml, "ParticleGravity", If(emitter.Is3D, New Vector3(0, -9.81F, 0), New Vector3(0, 980, 0)))
            emitter.Simulation.LinearDrag = GetAttributeDouble(xml, "LinearDrag", 0)
            Return emitter
        End Function

        Private Sub ApplyPhysicsProperties(element As SceneElement, xml As XmlElement)
            element.RigidBody = GetAttributeBoolean(xml, "RigidBody", GetAttributeBoolean(xml, "RidgidBody", False))
            element.FreeMove = GetAttributeBoolean(xml, "FreeMove", False)
            element.CollisionEnabled = GetAttributeBoolean(xml, "CollisionEnabled", False)
            If xml.HasAttribute("Mass") OrElse xml.HasAttribute("Weight") Then element.Mass = GetAttributeDouble(xml, "Mass", GetAttributeDouble(xml, "Weight", 1))
            If xml.HasAttribute("Bounce") Then element.Bounce = GetAttributeDouble(xml, "Bounce", 0)
            If xml.HasAttribute("Friction") Then element.Friction = GetAttributeDouble(xml, "Friction", 0.5)
            Dim friction As FrictionType
            If [Enum].TryParse(GetAttributeString(xml, "FrictionType", ""), True, friction) Then element.FrictionType = friction
            Dim motion As PhysicsMotion
            If [Enum].TryParse(GetAttributeString(xml, "PhysicsMotion", ""), True, motion) Then element.PhysicsMotion = motion
            element.Gravity = PhysicsVector2(xml, "Gravity", element.Gravity)
            If xml.HasAttribute("Velocity") Then element.Velocity = PhysicsVector2(xml, "Velocity", Vector2.Zero)
        End Sub

        Private Function PhysicsVector2(xml As XmlElement, name As String, fallback As Vector2) As Vector2
            Dim value = PhysicsVector(xml, name, 2)
            Return If(value Is Nothing, fallback, New Vector2(value(0), value(1)))
        End Function

        Private Function PhysicsVector3(xml As XmlElement, name As String, fallback As Vector3) As Vector3
            Dim value = PhysicsVector(xml, name, 3)
            Return If(value Is Nothing, fallback, New Vector3(value(0), value(1), value(2)))
        End Function

        Private Function PhysicsVector(xml As XmlElement, name As String, count As Integer) As Single()
            Dim parts = GetAttributeString(xml, name, "").Split({","c, " "c}, StringSplitOptions.RemoveEmptyEntries)
            If parts.Length <> count Then Return Nothing
            Dim result(count - 1) As Single
            For index = 0 To count - 1
                If Not Single.TryParse(parts(index), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, result(index)) OrElse Not Single.IsFinite(result(index)) Then Return Nothing
            Next
            Return result
        End Function
    End Class
End Namespace
