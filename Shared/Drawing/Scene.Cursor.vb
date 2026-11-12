Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Extensions
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing
    ''' <summary>System cursor shapes available to scene controls and hosts.</summary>
    Public Enum SceneCursor
        Arrow
        Hand
        IBeam
        SizeWE
        SizeNS
        SizeNWSE
        SizeNESW
        SizeAll
        Wait
        Cross
    End Enum

    Partial Public Class Scene
        ''' <summary>Resolves the cursor at a scene point, honoring pointer capture and explicit ancestor overrides.</summary>
        Public Function ResolveCursor(point As Point) As SceneCursor
            Dim captured = CapturedPointerElement
            Dim hasCapture = IsElementAttachedOrQueued(captured) AndAlso IsEffectivelyVisible(captured) AndAlso IsEffectivelyEnabled(captured)
            Dim target = If(hasCapture, captured, PointToElement(point, includeDisabled:=True))
            If target Is Nothing OrElse Not IsEffectivelyEnabled(target) Then Return SceneCursor.Arrow

            Dim inferred As SceneCursor? = If(hasCapture, pointerCaptureResizeCursor, Nothing)
            Dim current = target
            While current IsNot Nothing
                If current.CursorOverride.HasValue Then Return current.CursorOverride.Value
                If Not inferred.HasValue Then inferred = InferCursor(current, point)
                current = current.Parent
            End While
            Return inferred.GetValueOrDefault(SceneCursor.Arrow)
        End Function

        Private Shared Function InferCursor(element As SceneElement, point As Point) As SceneCursor?
            Dim resize = ResizeCursorFor(element, point)
            If resize.HasValue Then Return resize
            Dim text = TryCast(element, TextElement)
            If TypeOf element Is Textbox OrElse (text IsNot Nothing AndAlso text.IsTextSelectionEnabled) Then Return SceneCursor.IBeam
            If element.clickActionExists() OrElse TypeOf element Is Button OrElse TypeOf element Is CheckBox OrElse TypeOf element Is RadioButton OrElse
               TypeOf element Is ToggleSwitch OrElse TypeOf element Is ComboBox Then Return SceneCursor.Hand
            Return Nothing
        End Function

        Private Shared Function ResizeCursorFor(element As SceneElement, point As Point) As SceneCursor?
            Dim surface = TryCast(element, RectangleElement)
            If surface Is Nothing Then Return Nothing
            Select Case surface.ResizeGripAt(point.Subtract(surface.Position))
                Case CursorType.ResizeLeft, CursorType.ResizeRight : Return SceneCursor.SizeWE
                Case CursorType.ResizeTop, CursorType.ResizeBottom : Return SceneCursor.SizeNS
                Case Else : Return Nothing
            End Select
        End Function
    End Class
End Namespace
