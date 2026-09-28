Imports System.Collections.Generic
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Imports ProjectZ.Shared.Drawing.UI.Advanced

Namespace [Shared].Drawing.UI.Primitives

    <Serializable>
    Public Class CircleElement
        Inherits Advanced.PolygonElement

#Region "Properties"

        Public Property CenterPoint As Point
            Get
                Return _CenterPoint
            End Get
            Set(value As Point)
                _CenterPoint = value
                CalculatePoints()
            End Set
        End Property
        Private _CenterPoint As Point

        Public Property PointCount As Integer
            Get
                Return _PointCount
            End Get
            Set(value As Integer)
                _PointCount = value
                CalculatePoints()
            End Set
        End Property
        Private _PointCount As Integer = 100

#End Region

#Region "Constructors"

        Public Sub New(Scene As Scene)
            MyBase.New(Scene)
            Init()
        End Sub

        Public Sub New(Scene As Scene, newSpriteBatch As Boolean)
            MyBase.New(Scene, newSpriteBatch)
            Init()
        End Sub

        Public Sub New(Scene As Scene, spriteBatch As SpriteBatch)
            MyBase.New(Scene, spriteBatch)
            Init()
        End Sub

        Private Sub Init()
            CanChange = False
            Size = New Vector2(400)
            CenterPoint = New Point(CInt(Size.X), CInt(Size.Y))
            CanChange = True
            CalculatePoints()
        End Sub

#End Region

        Private Sub CalculatePoints()
            If Not CanChange Then Return
            CanChange = False
            Dim Points As New List(Of Vector2)
            Dim reoccurences As Integer = 0
            For i As Integer = 0 To PointCount - 1
                Dim x As Double = _CenterPoint.X + Size.X * Math.Cos(2 * Math.PI * i / PointCount) / 2 - (_CenterPoint.X / 2)
                Dim y As Double = _CenterPoint.Y + Size.Y * Math.Sin(2 * Math.PI * i / PointCount) / 2 - (_CenterPoint.Y / 2)
                Dim p As New Vector2(CSng(x), CSng(y))
                Dim lp As Vector2 = If(Points.Count > 0, Points(i - 1), Nothing)
                If lp <> Nothing AndAlso (lp.X = p.X And lp.Y = p.Y) Then
                    reoccurences += 1
                End If
                Points.Add(p)
            Next

            If reoccurences < PointCount - 2 Then
                AddVectorPoints(Points.ToArray)
            End If
            CanChange = True
        End Sub

        Private CanChange As Boolean = True, doCalculateOnNextDraw As Boolean = False
        Private Sub CircleElement_OnSizeChange(oldSize As Vector2, newSize As Vector2) Handles Me.SizeChanged
            If CanChange Then
                _CenterPoint = New Point(CInt(newSize.X), CInt(newSize.Y))
                doCalculateOnNextDraw = True
            End If
        End Sub

        Private Sub CircleElement_PreDraw(gameTime As GameTime) Handles Me.PreDraw
            If doCalculateOnNextDraw And CanChange Then
                doCalculateOnNextDraw = False
                CalculatePoints()
            End If
        End Sub

        Private Sub CircleElement_Trangulated(newMesh As PolygonMesh) Handles Me.Trangulated

        End Sub
    End Class

End Namespace
