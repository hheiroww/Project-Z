Imports System.Globalization
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Input
    ''' <summary>Finite, bounded, quantized value model shared by native studio controls.</summary>
    Public NotInheritable Class NumericRange
        Private low As Double, high As Double = 1, current As Double
        Public Property StepSize As Double = 0.01
        Public Event Changed(value As Double)
        Public Property Minimum As Double
            Get
                Return low
            End Get
            Set(value As Double)
                If Not Double.IsFinite(value) Then Throw New ArgumentOutOfRangeException(NameOf(value))
                low = value : high = Math.Max(high, low) : Me.Value = current
            End Set
        End Property
        Public Property Maximum As Double
            Get
                Return high
            End Get
            Set(value As Double)
                If Not Double.IsFinite(value) Then Throw New ArgumentOutOfRangeException(NameOf(value))
                high = value : low = Math.Min(low, high) : Me.Value = current
            End Set
        End Property
        Public Property Value As Double
            Get
                Return current
            End Get
            Set(value As Double)
                If Not Double.IsFinite(value) Then Return
                Dim bounded = Math.Clamp(value, low, high)
                If Double.IsFinite(StepSize) AndAlso StepSize > 0 Then bounded = Math.Clamp(low + Math.Round((bounded - low) / StepSize) * StepSize, low, high)
                If Math.Abs(current - bounded) < 0.000000001 Then Return
                current = bounded : RaiseEvent Changed(current)
            End Set
        End Property
        Public ReadOnly Property Fraction As Single
            Get
                Return CSng(If(high > low, (current - low) / (high - low), 0))
            End Get
        End Property
    End Class

    ''' <summary>GPU drawn value control with captured vertical drag, wheel and keyboard input.</summary>
    Public MustInherit Class StudioValueControl
        Inherits RectangleElement
        Protected ReadOnly Range As New NumericRange
        Private startValue As Double, startY As Single
        Public Property AccentColor As Color = New Color(255, 155, 66)
        Public Property TrackColor As Color = New Color(58, 61, 65)
        Public Property DragPixels As Double = 180
        Public Event ValueChanged(value As Double)
        Public Property MinimumValue As Double
            Get
                Return Range.Minimum
            End Get
            Set(value As Double)
                Range.Minimum = value
            End Set
        End Property
        Public Property MaximumValue As Double
            Get
                Return Range.Maximum
            End Get
            Set(value As Double)
                Range.Maximum = value
            End Set
        End Property
        Public Property StepSize As Double
            Get
                Return Range.StepSize
            End Get
            Set(value As Double)
                Range.StepSize = value
            End Set
        End Property
        Public Property Value As Double
            Get
                Return Range.Value
            End Get
            Set(value As Double)
                Range.Value = value
            End Set
        End Property
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            CanSelect = True : TabStop = True : BackgroundColor = New Color(27, 29, 33)
            AddHandler Range.Changed, Sub(v)
                                          Scene.RequestRender() : RaiseEvent ValueChanged(v)
                                      End Sub
            AddHandler MouseLeftDown, Sub(p)
                                          startValue = Value : startY = Position.Y + p.Y
                                      End Sub
            AddHandler scene.OnMouseMove, AddressOf Drag
            AddHandler MouseWheel, Sub(delta, p) Value += Math.Sign(delta) * EffectiveStep()
            AddHandler OnKeyDown, AddressOf Key
        End Sub
        Private Function EffectiveStep() As Double
            Dim fine = Keyboard.GetState().IsKeyDown(Keys.LeftShift) OrElse Keyboard.GetState().IsKeyDown(Keys.RightShift)
            Return If(Range.StepSize > 0, Range.StepSize, (MaximumValue - MinimumValue) / 100) * If(fine, 0.1, 1)
        End Function
        Private Sub Key(key As Keys, state As KeyboardState)
            If key = Keys.Up OrElse key = Keys.Right Then Value += EffectiveStep()
            If key = Keys.Down OrElse key = Keys.Left Then Value -= EffectiveStep()
            If key = Keys.Home Then Value = MinimumValue
            If key = Keys.End Then Value = MaximumValue
        End Sub
        Private Sub Drag(current As Point, previous As Point)
            If Not isMouseDown OrElse Not isEnabled Then Return
            Dim fine = Keyboard.GetState().IsKeyDown(Keys.LeftShift) OrElse Keyboard.GetState().IsKeyDown(Keys.RightShift)
            SetDragValue(startValue, startY, current.Y, If(fine, 0.1, 1))
        End Sub
        Protected Overridable Sub SetDragValue(initial As Double, initialY As Single, y As Single, scale As Double)
            Value = initial + (initialY - y) / Math.Max(1, DragPixels) * (MaximumValue - MinimumValue) * scale
        End Sub
        Protected Sub Line(a As Vector2, b As Vector2, width As Single, color As Color)
            Dim d = b - a
            spriteBatch.Draw(Texture, a, Nothing, ApplyOpacity(color), CSng(Math.Atan2(d.Y, d.X)),
                             New Vector2(0, 0.5F), New Vector2(d.Length(), width), SpriteEffects.None, 0)
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then RemoveHandler Scene.OnMouseMove, AddressOf Drag
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ''' <summary>270-degree min/max knob. Drag vertically; Shift slows movement. Home/End select limits.</summary>
    Public Class RotaryKnob
        Inherits StudioValueControl
        Public Sub New(scene As Scene)
            MyBase.New(scene) : Size = New Vector2(60, 60) : CornerRadius = 30
        End Sub
        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            Dim bodyRadius = Math.Min(Size.X, Size.Y) / 2
            If CornerRadius <> bodyRadius Then CornerRadius = bodyRadius
            MyBase.Draw(gameTime)
            Dim center = Position + Size / 2
            Dim radius = Math.Max(2, Math.Min(Size.X, Size.Y) / 2 - 5)
            Dim angle = CSng(Math.PI * (0.75 + Range.Fraction * 1.5))
            For i = 0 To 63
                Dim a = CSng(Math.PI * (0.75 + i / 64.0 * 1.5)), b = CSng(Math.PI * (0.75 + (i + 1) / 64.0 * 1.5))
                Line(center + New Vector2(CSng(Math.Cos(a)), CSng(Math.Sin(a))) * radius,
                     center + New Vector2(CSng(Math.Cos(b)), CSng(Math.Sin(b))) * radius, 4,
                     If(i / 64.0 <= Range.Fraction, AccentColor, TrackColor))
            Next
            ' Solid machined center and a restrained rim; no decorative value gradient.
            For i = 0 To 47
                Dim a = CSng(i * Math.PI * 2 / 48), b = CSng((i + 1) * Math.PI * 2 / 48)
                Line(center + New Vector2(CSng(Math.Cos(a)), CSng(Math.Sin(a))) * (radius - 6),
                     center + New Vector2(CSng(Math.Cos(b)), CSng(Math.Sin(b))) * (radius - 6), 3, BackgroundColor)
            Next
            Dim direction As New Vector2(CSng(Math.Cos(angle)), CSng(Math.Sin(angle)))
            Line(center + direction * 5, center + direction * (radius - 8), 3, If(isMouseOver OrElse isSelected, Color.White, AccentColor))
        End Sub
    End Class

    ''' <summary>Absolute vertical fader with ticks and a physical thumb.</summary>
    Public Class VerticalSlider
        Inherits StudioValueControl
        Public Property TickCount As Integer = 9
        Public Sub New(scene As Scene)
            MyBase.New(scene) : Size = New Vector2(40, 160) : CornerRadius = 4
            AddHandler MouseLeftDown, Sub(p) SetDragValue(0, 0, Position.Y + p.Y, 1)
        End Sub
        Protected Overrides Sub SetDragValue(initial As Double, initialY As Single, y As Single, scale As Double)
            If scale < 1 Then
                MyBase.SetDragValue(initial, initialY, y, scale)
            Else
                Value = MinimumValue + Math.Clamp(1 - (y - Position.Y - 9) / Math.Max(1, Size.Y - 18), 0, 1) * (MaximumValue - MinimumValue)
            End If
        End Sub
        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            MyBase.Draw(gameTime)
            Dim x = Position.X + Size.X / 2, top = Position.Y + 9, bottom = Position.Y + Size.Y - 9
            Line(New Vector2(x, top), New Vector2(x, bottom), 4, TrackColor)
            Dim y = bottom - Range.Fraction * (bottom - top)
            Line(New Vector2(x, bottom), New Vector2(x, y), 2, AccentColor)
            For i = 0 To Math.Clamp(TickCount, 2, 32) - 1
                Dim tick = top + i / CSng(Math.Clamp(TickCount, 2, 32) - 1) * (bottom - top)
                Line(New Vector2(x + 8, tick), New Vector2(x + 13, tick), 1, TrackColor)
            Next
            spriteBatch.Draw(Texture, New Rectangle(CInt(x - 8), CInt(y - 9), 16, 18), ApplyOpacity(AccentColor))
            Line(New Vector2(x - 6, y), New Vector2(x + 6, y), 2, New Color(30, 31, 34))
        End Sub
    End Class

    ''' <summary>Numeric text entry with vertical dragging, arrow-key stepping, wheel and bounded parsing.</summary>
    Public Class DragNumberBox
        Inherits Textbox
        Private ReadOnly range As New NumericRange
        Private startValue As Double, startY As Single, moved As Boolean
        Public Property DecimalPlaces As Integer = 2
        Public Property AccentColor As Color = New Color(255, 155, 66)
        Public Property ShowSpinButtons As Boolean = True
        Public Event ValueChanged(value As Double)
        Public Property MinimumValue As Double
            Get
                Return range.Minimum
            End Get
            Set(value As Double)
                range.Minimum = value
            End Set
        End Property
        Public Property MaximumValue As Double
            Get
                Return range.Maximum
            End Get
            Set(value As Double)
                range.Maximum = value
            End Set
        End Property
        Public Property StepSize As Double
            Get
                Return range.StepSize
            End Get
            Set(value As Double)
                range.StepSize = value
            End Set
        End Property
        Public Property Value As Double
            Get
                Return range.Value
            End Get
            Set(value As Double)
                range.Value = value : RefreshNumber()
            End Set
        End Property
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            AcceptsReturn = False : Size = New Vector2(96, 32) : CornerRadius = 5
            BackgroundColor = New Color(23, 25, 28) : ForegroundColor = Color.White : BorderThickness = 1
            AddHandler range.Changed, Sub(v)
                                          RefreshNumber() : Scene.RequestRender() : RaiseEvent ValueChanged(v)
                                      End Sub
            AddHandler Submitted, AddressOf Commit
            AddHandler Deselected, AddressOf Commit
            AddHandler MouseLeftDown, Sub(p)
                                          startValue = Value : startY = Position.Y + p.Y : moved = False
                                          If ShowSpinButtons AndAlso p.X >= Size.X - 18 Then
                                              Value += If(p.Y < Size.Y / 2, range.StepSize, -range.StepSize)
                                              startValue = Value
                                          End If
                                      End Sub
            AddHandler scene.OnMouseMove, AddressOf Drag
            AddHandler MouseWheel, Sub(delta, p) Value += Math.Sign(delta) * range.StepSize
            AddHandler OnKeyDown, Sub(key, state)
                                     If key = Keys.Up Then Value += range.StepSize
                                     If key = Keys.Down Then Value -= range.StepSize
                                     If key = Keys.Escape Then RefreshNumber()
                                 End Sub
            RefreshNumber()
        End Sub
        Private Sub Commit()
            Dim parsed As Double
            If Double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then Value = parsed
            RefreshNumber()
        End Sub
        Private Sub RefreshNumber()
            Text = Value.ToString("F" & Math.Clamp(DecimalPlaces, 0, 8), CultureInfo.InvariantCulture)
        End Sub
        Private Sub Drag(current As Point, previous As Point)
            If Not isMouseDown OrElse Not isEnabled Then Return
            Dim delta = startY - current.Y
            If Math.Abs(delta) < 4 AndAlso Not moved Then Return
            moved = True
            Dim fine = Keyboard.GetState().IsKeyDown(Keys.LeftShift) OrElse Keyboard.GetState().IsKeyDown(Keys.RightShift)
            Value = startValue + delta * range.StepSize * If(fine, 0.1, 1)
        End Sub
        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            BorderColor = If(isMouseOver OrElse isSelected, AccentColor, New Color(65, 69, 74))
            MyBase.Draw(gameTime)
            If Not ShowSpinButtons Then Return
            Dim x = CInt(Position.X + Size.X - 9), top = CInt(Position.Y + Size.Y / 2 - 9), bottom = CInt(Position.Y + Size.Y / 2 + 5)
            For i = 0 To 2
                spriteBatch.Draw(Texture, New Rectangle(x - i, top + i, i * 2 + 1, 1), ApplyOpacity(AccentColor))
                spriteBatch.Draw(Texture, New Rectangle(x - 2 + i, bottom + i, 5 - i * 2, 1), ApplyOpacity(AccentColor))
            Next
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then RemoveHandler Scene.OnMouseMove, AddressOf Drag
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ''' <summary>Semantic dB meter: -48 red, -24 yellow, 0 green. Values are clamped to the scale.</summary>
    Public Class DecibelMeter
        Inherits RectangleElement
        Public Property Decibels As Double = -48
        Public Sub New(scene As Scene)
            MyBase.New(scene) : Size = New Vector2(12, 160) : isMouseBypassEnabled = True
        End Sub
        Public Shared Function ColorAt(db As Double) As Color
            Dim value = Math.Clamp(If(Double.IsFinite(db), db, -48), -48, 0)
            Return If(value <= -24, Color.Lerp(New Color(230, 75, 59), New Color(249, 205, 66), CSng((value + 48) / 24)),
                      Color.Lerp(New Color(249, 205, 66), New Color(83, 213, 134), CSng((value + 24) / 24)))
        End Function
        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            For y = 0 To Math.Max(0, CInt(Size.Y) - 1)
                Dim db = -48 + 48 * (1 - y / Math.Max(1.0, Size.Y - 1))
                Dim color = If(db <= Decibels, ColorAt(db), New Color(38, 41, 45))
                spriteBatch.Draw(Texture, New Rectangle(CInt(Position.X), CInt(Position.Y) + y, CInt(Size.X), 1), ApplyOpacity(color))
            Next
        End Sub
    End Class

    ''' <summary>Solid-color native button with an on-demand GPU accent glow on hover/focus.</summary>
    Public Class AccentButton
        Inherits Button
        Public Property AccentColor As Color = New Color(255, 155, 66)
        Private ReadOnly glow As New XamlNativeEffect With {.Kind = XamlEffectKind.DropShadow, .Radius = 4, .ShadowDepth = 0, .Opacity = 0.2F}
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            AddHandler PreDraw, Sub(time)
                                    Dim active = isMouseOver OrElse isSelected OrElse isMouseDown
                                    glow.Color = AccentColor
                                    VisualEffect = If(active AndAlso isEnabled, glow, Nothing)
                                End Sub
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                VisualEffect = Nothing : glow.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
