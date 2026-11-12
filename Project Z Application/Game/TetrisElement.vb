Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Content
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.UI

''' <summary>
''' A SceneElement that renders and controls a playable Tetris game.
''' </summary>
Public Class TetrisElement
    Inherits SceneElement

#Region "Properties"

    Private Game As Tetris
    Private WhiteTexture As Texture2D
    Private _Scene As Scene
    Private _Font As SpriteFont

    ''' <summary>Number of grid columns.</summary>
    Public Property GridColumns As Integer = 12

    ''' <summary>Number of grid rows.</summary>
    Public Property GridRows As Integer = 24

    ''' <summary>Size of each cell in pixels.</summary>
    Public Property CellSize As Integer = 24

    ''' <summary>Color of the grid border lines.</summary>
    Public Property GridLineColor As Color = New Color(40, 40, 40)

    ''' <summary>Background color of the play area.</summary>
    Public Property BoardColor As Color = New Color(15, 15, 15)

    ''' <summary>Color of the border around the play area.</summary>
    Public Property BorderColor As Color = New Color(80, 80, 80)

    Public Property LabelColor As Color = Color.White
    Public Property LabelAccentColor As Color = Color.Gray

    ''' <summary>Border thickness in pixels.</summary>
    Public Property BorderThickness As Integer = 2

    ''' <summary>Interval in milliseconds between automatic drops.</summary>
    Public Property DropInterval As Double = 500

    ''' <summary>Score display text.</summary>
    Public ReadOnly Property Score As Integer
        Get
            Return _Score
        End Get
    End Property
    Private _Score As Integer = 0

    ''' <summary>Lines cleared count.</summary>
    Public ReadOnly Property LinesCleared As Integer
        Get
            Return _LinesCleared
        End Get
    End Property
    Private _LinesCleared As Integer = 0

    ''' <summary>Whether the game is over.</summary>
    Public ReadOnly Property IsGameOver As Boolean
        Get
            Return _IsGameOver
        End Get
    End Property
    Private _IsGameOver As Boolean = False

    ''' <summary>Whether the game is paused.</summary>
    Public Property IsPaused As Boolean = False

    Private _DropTimer As Double = 0
    Private _LastKeyboardState As KeyboardState
    Private _KeyRepeatTimer As Double = 0
    Private _KeyRepeatDelay As Double = 200  ' ms before repeat starts
    Private _KeyRepeatRate As Double = 50    ' ms between repeats
    Private _IsRepeating As Boolean = False
    Private _LastRepeatedKey As Keys = Keys.None

    ' Ghost piece support
    Public Property ShowGhostPiece As Boolean = True
    Public Property GhostPieceAlpha As Integer = 40

    ' Next piece preview dimensions
    Private Const PreviewCells As Integer = 5
    Private Const PreviewOffsetX As Integer = 12

#End Region

#Region "Events"

    Public Event ScoreChanged(newScore As Integer)
    Public Event GameOverEvent()
    Public Event LineCompleted(linesCleared As Integer)

#End Region

#Region "Constructors"

    Public Sub New(scene As Scene)
        MyBase.New(scene)
        _Scene = scene
        InitializeGame()
    End Sub

    Public Sub New(scene As Scene, columns As Integer, rows As Integer, cellSize As Integer)
        MyBase.New(scene)
        _Scene = scene
        Me.GridColumns = columns
        Me.GridRows = rows
        Me.CellSize = cellSize
        InitializeGame()
    End Sub

    Private Sub InitializeGame()
        Game = New Tetris(New Point(GridColumns, GridRows))
        ' Set element size to include the board + preview area + border
        Dim boardWidth As Integer = GridColumns * CellSize + BorderThickness * 2
        Dim previewWidth As Integer = PreviewCells * CellSize + CellSize
        Me.Size = New Vector2(boardWidth + previewWidth, GridRows * CellSize + BorderThickness * 2)
        Me.CanSelect = True
        AddHandler Game.NextBlockChanged, AddressOf OnNextBlockChanged
    End Sub

    Private Function GetGraphicsDevice() As GraphicsDevice
        Return spriteBatch.GraphicsDevice
    End Function

    Private Function GetFont() As SpriteFont
        If _Font IsNot Nothing Then Return _Font
        Try
            Dim container As ContentContainer = CType(_Scene.sender().Services.GetService(GetType(ContentContainer)), ContentContainer)
            If container IsNot Nothing Then
                Dim fontName As String = Fonts.SegoeUI.GetResourceName(12)
                If container.Fonts.ContainsKey(fontName) Then
                    _Font = container.Fonts(fontName)
                End If
            End If
        Catch
        End Try
        Return _Font
    End Function

#End Region

#Region "Game Control"

    ''' <summary>
    ''' Restarts the game from scratch.
    ''' </summary>
    Public Sub Restart()
        RemoveHandler Game.NextBlockChanged, AddressOf OnNextBlockChanged
        Game = New Tetris(New Point(GridColumns, GridRows))
        AddHandler Game.NextBlockChanged, AddressOf OnNextBlockChanged
        _Score = 0
        _LinesCleared = 0
        _IsGameOver = False
        IsPaused = False
        _DropTimer = 0
    End Sub

    Private Sub OnNextBlockChanged(nextBlock As TetrisBlock)
        ' Event from game logic when next block changes
    End Sub

    ''' <summary>
    ''' Performs a hard drop — instantly drops the current block to the bottom.
    ''' </summary>
    Private Sub HardDrop()
        Dim current = Game.GetCurrentBlock()
        If current Is Nothing Then Return

        Dim dropDistance As Integer = 0
        While Game.CanIncrement(current)
            current.Increment()
            dropDistance += 1
        End While
        current.HasReachedBottom = True
        _Score += dropDistance * 2
        RaiseEvent ScoreChanged(_Score)
    End Sub

    ''' <summary>
    ''' Calculates the ghost piece drop distance for the current block.
    ''' </summary>
    Private Function GetGhostDropDistance(block As TetrisBlock) As Integer
        If block Is Nothing OrElse block.blockData.Count = 0 Then Return 0

        ' Work with a copy to avoid mutating the real block
        Dim points As New List(Of Point)(block.blockData)
        Dim distance As Integer = 0

        While True
            Dim canDrop As Boolean = True
            For Each p As Point In points
                Dim nextY As Integer = p.Y + 1
                If nextY >= GridRows Then
                    canDrop = False
                    Exit For
                End If
                For Each other As TetrisBlock In Game.blockData
                    If other IsNot block AndAlso other.HasReachedBottom Then
                        For Each op As Point In other.blockData
                            If op.X = p.X AndAlso op.Y = nextY Then
                                canDrop = False
                                Exit For
                            End If
                        Next
                    End If
                    If Not canDrop Then Exit For
                Next
                If Not canDrop Then Exit For
            Next

            If canDrop Then
                For i As Integer = 0 To points.Count - 1
                    points(i) = New Point(points(i).X, points(i).Y + 1)
                Next
                distance += 1
            Else
                Exit While
            End If
        End While

        Return distance
    End Function

#End Region

#Region "Update (Tick)"

    Public Overrides Sub Tick(gameTime As GameTime)
        Try
            MyBase.Tick(gameTime)

            If _IsGameOver OrElse IsPaused Then
                ' Still handle restart/pause keys when game over or paused
                HandlePauseRestartInput()
                Return
            End If
            If Game Is Nothing Then Return

            Dim elapsed As Double = gameTime.ElapsedGameTime.TotalMilliseconds

            ' Handle keyboard input
            HandleInput(gameTime)

            ' Auto-drop timer
            _DropTimer += elapsed
            If _DropTimer >= DropInterval Then
                _DropTimer = 0
                PerformTick()
            End If
        Catch ex As Exception
            Debug.WriteLine($"TetrisElement.Tick error: {ex.Message}")
        End Try
    End Sub

    Private Sub HandlePauseRestartInput()
        Dim kbState = _Scene.InputKeyboardState

        If IsNewKeyPress(Keys.R, kbState) Then
            Restart()
        End If

        If IsNewKeyPress(Keys.P, kbState) OrElse IsNewKeyPress(Keys.Escape, kbState) Then
            If Not _IsGameOver Then
                IsPaused = Not IsPaused
            End If
        End If

        _LastKeyboardState = kbState
    End Sub

    Private Sub PerformTick()
        Dim hadActiveBlock As Boolean = (Game.GetCurrentBlock() IsNot Nothing)

        Game.Tick()

        ' Check for game over: a block just settled and a new one spawned —
        ' if the new block overlaps any settled block, the board is full.
        Dim current = Game.GetCurrentBlock()
        If current IsNot Nothing AndAlso Not current.HasReachedBottom Then
            For Each p As Point In current.blockData
                For Each other As TetrisBlock In Game.blockData
                    If other IsNot current AndAlso other.HasReachedBottom Then
                        For Each op As Point In other.blockData
                            If op.X = p.X AndAlso op.Y = p.Y Then
                                _IsGameOver = True
                                RaiseEvent GameOverEvent()
                                Return
                            End If
                        Next
                    End If
                Next
            Next
        End If

        ' Detect line clears — count filled rows after tick
        Dim rowCounts As New Dictionary(Of Integer, Integer)
        For Each b As TetrisBlock In Game.blockData
            If b.HasReachedBottom Then
                For Each p As Point In b.blockData
                    If rowCounts.ContainsKey(p.Y) Then
                        rowCounts(p.Y) += 1
                    Else
                        rowCounts.Add(p.Y, 1)
                    End If
                Next
            End If
        Next

        Dim cleared As Integer = 0
        For Each kvp In rowCounts
            If kvp.Value >= GridColumns Then cleared += 1
        Next

        If cleared > 0 Then
            _LinesCleared += cleared
            Select Case cleared
                Case 1 : _Score += 100
                Case 2 : _Score += 300
                Case 3 : _Score += 500
                Case 4 : _Score += 800
            End Select
            RaiseEvent LineCompleted(cleared)
            RaiseEvent ScoreChanged(_Score)
            DropInterval = Math.Max(100, DropInterval - 5)
        End If
    End Sub

    Private Sub HandleInput(gameTime As GameTime)
        Dim kbState = _Scene.InputKeyboardState
        Dim elapsed As Double = gameTime.ElapsedGameTime.TotalMilliseconds
        Dim current = Game.GetCurrentBlock()

        If current Is Nothing OrElse current.HasReachedBottom Then
            _LastKeyboardState = kbState
            Return
        End If

        ' Single-press keys (no repeat)
        If IsNewKeyPress(Keys.Up, kbState) OrElse IsNewKeyPress(Keys.X, kbState) Then
            If Game.CanRotate(current, TetrisBlock.Direction.Right) Then
                current.Rotate(TetrisBlock.Direction.Right)
            End If
        End If

        If IsNewKeyPress(Keys.Z, kbState) Then
            If Game.CanRotate(current, TetrisBlock.Direction.Left) Then
                current.Rotate(TetrisBlock.Direction.Left)
            End If
        End If

        If IsNewKeyPress(Keys.Space, kbState) Then
            HardDrop()
        End If

        If IsNewKeyPress(Keys.P, kbState) OrElse IsNewKeyPress(Keys.Escape, kbState) Then
            IsPaused = Not IsPaused
        End If

        If IsNewKeyPress(Keys.R, kbState) Then
            Restart()
            _LastKeyboardState = kbState
            Return
        End If

        ' Repeating keys (Left, Right, Down)
        HandleRepeatingKey(Keys.Left, kbState, elapsed, Sub()
                                                            If Game.CanIncrement(current, New Point(-1, 0)) Then
                                                                current.Move(TetrisBlock.Direction.Left)
                                                            End If
                                                        End Sub)

        HandleRepeatingKey(Keys.Right, kbState, elapsed, Sub()
                                                             If Game.CanIncrement(current, New Point(1, 0)) Then
                                                                 current.Move(TetrisBlock.Direction.Right)
                                                             End If
                                                         End Sub)

        HandleRepeatingKey(Keys.Down, kbState, elapsed, Sub()
                                                            If Game.CanIncrement(current) Then
                                                                current.Increment()
                                                                _Score += 1
                                                                _DropTimer = 0
                                                            End If
                                                        End Sub)

        _LastKeyboardState = kbState
    End Sub

    Private Function IsNewKeyPress(key As Keys, currentState As KeyboardState) As Boolean
        Return currentState.IsKeyDown(key) AndAlso _LastKeyboardState.IsKeyUp(key)
    End Function

    Private Sub HandleRepeatingKey(key As Keys, kbState As KeyboardState, elapsed As Double, action As Action)
        If kbState.IsKeyDown(key) Then
            If _LastKeyboardState.IsKeyUp(key) Then
                action()
                _LastRepeatedKey = key
                _KeyRepeatTimer = 0
                _IsRepeating = False
            ElseIf _LastRepeatedKey = key Then
                _KeyRepeatTimer += elapsed
                If Not _IsRepeating Then
                    If _KeyRepeatTimer >= _KeyRepeatDelay Then
                        _IsRepeating = True
                        _KeyRepeatTimer = 0
                        action()
                    End If
                Else
                    If _KeyRepeatTimer >= _KeyRepeatRate Then
                        _KeyRepeatTimer = 0
                        action()
                    End If
                End If
            End If
        Else
            If _LastRepeatedKey = key Then
                _LastRepeatedKey = Keys.None
                _IsRepeating = False
                _KeyRepeatTimer = 0
            End If
        End If
    End Sub

#End Region

#Region "Drawing"

    Protected Overrides Sub Draw(gameTime As GameTime)
        If WhiteTexture Is Nothing Then
            WhiteTexture = Textures.CreateSolidTexture(GetGraphicsDevice(), Color.White)
        End If

        Dim boardX As Integer = Rectangle.X + BorderThickness
        Dim boardY As Integer = Rectangle.Y + BorderThickness
        Dim boardWidth As Integer = GridColumns * CellSize
        Dim boardHeight As Integer = GridRows * CellSize

        ' Draw board background
        spriteBatch.Draw(WhiteTexture, New Rectangle(boardX, boardY, boardWidth, boardHeight), BoardColor)

        ' Draw grid lines
        DrawGridLines(boardX, boardY, boardWidth, boardHeight)

        ' Draw settled blocks
        If Game IsNot Nothing Then
            For Each block As TetrisBlock In Game.blockData
                If block.HasReachedBottom Then
                    DrawBlock(block, boardX, boardY, block.Color)
                End If
            Next

            ' Draw ghost piece
            Dim current = Game.GetCurrentBlock()
            If current IsNot Nothing AndAlso ShowGhostPiece Then
                Dim ghostDistance As Integer = GetGhostDropDistance(current)
                If ghostDistance > 0 Then
                    Dim ghostColor As New Color(current.Color.R, current.Color.G, current.Color.B, GhostPieceAlpha)
                    For Each p As Point In current.blockData
                        Dim ghostP As New Point(p.X, p.Y + ghostDistance)
                        If ghostP.Y >= 0 AndAlso ghostP.X >= 0 AndAlso ghostP.X < GridColumns AndAlso ghostP.Y < GridRows Then
                            Dim cellRect As New Rectangle(boardX + ghostP.X * CellSize, boardY + ghostP.Y * CellSize, CellSize, CellSize)
                            spriteBatch.Draw(WhiteTexture, cellRect, ghostColor)
                            DrawCellBorder(cellRect, New Color(ghostColor.R, ghostColor.G, ghostColor.B, CInt(GhostPieceAlpha * 1.5)))
                        End If
                    Next
                End If
            End If

            ' Draw current (active) block
            If current IsNot Nothing Then
                DrawBlock(current, boardX, boardY, current.Color)
            End If

            ' Draw next piece preview
            DrawNextPiecePreview(boardX + boardWidth + CellSize, boardY)
        End If

        ' Draw border around board
        DrawBorder(boardX - BorderThickness, boardY - BorderThickness,
                   boardWidth + BorderThickness * 2, boardHeight + BorderThickness * 2)

        ' Draw score and info text
        DrawInfoPanel(boardX + boardWidth + CellSize, boardY + PreviewCells * CellSize + CellSize * 2)

        ' Draw game over overlay
        If _IsGameOver Then
            DrawGameOverOverlay(boardX, boardY, boardWidth, boardHeight)
        End If

        ' Draw pause overlay
        If IsPaused AndAlso Not _IsGameOver Then
            DrawPauseOverlay(boardX, boardY, boardWidth, boardHeight)
        End If
    End Sub

    Private Sub DrawBlock(block As TetrisBlock, boardX As Integer, boardY As Integer, color As Color)
        For Each p As Point In block.blockData
            If p.Y >= 0 AndAlso p.X >= 0 AndAlso p.X < GridColumns AndAlso p.Y < GridRows Then
                Dim cellRect As New Rectangle(boardX + p.X * CellSize, boardY + p.Y * CellSize, CellSize, CellSize)

                ' Draw filled cell
                spriteBatch.Draw(WhiteTexture, cellRect, color)

                ' Draw highlight (top-left light effect)
                Dim highlightColor As New Color(
                    Math.Min(255, CInt(color.R) + 60),
                    Math.Min(255, CInt(color.G) + 60),
                    Math.Min(255, CInt(color.B) + 60), 200)
                spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Y, cellRect.Width, 2), highlightColor)
                spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Y, 2, cellRect.Height), highlightColor)

                ' Draw shadow (bottom-right dark effect)
                Dim shadowColor As New Color(
                    CInt(color.R * 0.5),
                    CInt(color.G * 0.5),
                    CInt(color.B * 0.5), 200)
                spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Bottom - 2, cellRect.Width, 2), shadowColor)
                spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.Right - 2, cellRect.Y, 2, cellRect.Height), shadowColor)
            End If
        Next
    End Sub

    Private Sub DrawCellBorder(cellRect As Rectangle, color As Color)
        spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Y, cellRect.Width, 1), color)
        spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Bottom - 1, cellRect.Width, 1), color)
        spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Y, 1, cellRect.Height), color)
        spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.Right - 1, cellRect.Y, 1, cellRect.Height), color)
    End Sub

    Private Sub DrawGridLines(boardX As Integer, boardY As Integer, boardWidth As Integer, boardHeight As Integer)
        ' Vertical lines
        For col As Integer = 0 To GridColumns
            Dim x As Integer = boardX + col * CellSize
            spriteBatch.Draw(WhiteTexture, New Rectangle(x, boardY, 1, boardHeight), GridLineColor)
        Next

        ' Horizontal lines
        For row As Integer = 0 To GridRows
            Dim y As Integer = boardY + row * CellSize
            spriteBatch.Draw(WhiteTexture, New Rectangle(boardX, y, boardWidth, 1), GridLineColor)
        Next
    End Sub

    Private Sub DrawBorder(x As Integer, y As Integer, width As Integer, height As Integer)
        spriteBatch.Draw(WhiteTexture, New Rectangle(x, y, width, BorderThickness), BorderColor)
        spriteBatch.Draw(WhiteTexture, New Rectangle(x, y + height - BorderThickness, width, BorderThickness), BorderColor)
        spriteBatch.Draw(WhiteTexture, New Rectangle(x, y, BorderThickness, height), BorderColor)
        spriteBatch.Draw(WhiteTexture, New Rectangle(x + width - BorderThickness, y, BorderThickness, height), BorderColor)
    End Sub

    Private Sub DrawNextPiecePreview(previewX As Integer, previewY As Integer)
        Dim previewSize As Integer = PreviewCells * CellSize

        ' Draw preview background
        spriteBatch.Draw(WhiteTexture, New Rectangle(previewX, previewY, previewSize, previewSize), BoardColor)

        ' Draw preview border
        DrawBorder(previewX - BorderThickness, previewY - BorderThickness,
                   previewSize + BorderThickness * 2, previewSize + BorderThickness * 2)

        ' Draw "NEXT" label
        Dim font As SpriteFont = GetFont()
        If font IsNot Nothing Then
            spriteBatch.DrawString(font, "NEXT", New Vector2(previewX + 2, previewY - 18), LabelAccentColor)
        End If

        ' Draw the next piece
        If Game IsNot Nothing AndAlso Game.spawnList.Count > 0 Then
            Dim nextBlock As TetrisBlock = Game.spawnList(0)
            Dim smallCellSize As Integer = CInt(CellSize * 0.8)

            ' Calculate offset to center the preview piece
            Dim blockSize As Point = nextBlock.CalculateSize()
            Dim offsetX As Integer = CInt((previewSize - (blockSize.X + 1) * smallCellSize) / 2)
            Dim offsetY As Integer = CInt((previewSize - (blockSize.Y + 1) * smallCellSize) / 2)

            ' Get the block's offset to normalize positions
            Dim blockOffset As Point = nextBlock.Offset()

            For Each p As Point In nextBlock.blockData
                Dim normalX As Integer = p.X - blockOffset.X
                Dim normalY As Integer = p.Y - blockOffset.Y
                Dim cellRect As New Rectangle(
                    previewX + offsetX + normalX * smallCellSize,
                    previewY + offsetY + normalY * smallCellSize,
                    smallCellSize, smallCellSize)

                spriteBatch.Draw(WhiteTexture, cellRect, nextBlock.Color)

                ' Mini highlight
                Dim highlightColor As New Color(
                    Math.Min(255, CInt(nextBlock.Color.R) + 60),
                    Math.Min(255, CInt(nextBlock.Color.G) + 60),
                    Math.Min(255, CInt(nextBlock.Color.B) + 60), 180)
                spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Y, cellRect.Width, 1), highlightColor)
                spriteBatch.Draw(WhiteTexture, New Rectangle(cellRect.X, cellRect.Y, 1, cellRect.Height), highlightColor)
            Next
        End If
    End Sub

    Private Sub DrawInfoPanel(x As Integer, y As Integer)
        Dim font As SpriteFont = GetFont()
        If font Is Nothing Then Return

        Dim lineHeight As Integer = 22
        Dim currentY As Integer = y

        spriteBatch.DrawString(font, "SCORE", New Vector2(x + 2, currentY), LabelAccentColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, _Score.ToString(), New Vector2(x + 2, currentY), LabelColor)
        currentY += lineHeight + 8

        spriteBatch.DrawString(font, "LINES", New Vector2(x + 2, currentY), LabelAccentColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, _LinesCleared.ToString(), New Vector2(x + 2, currentY), LabelColor)
        currentY += lineHeight + 16

        ' Controls help
        Dim helpColor = Color.Lerp(LabelColor, BoardColor, 0.2F)
        spriteBatch.DrawString(font, "CONTROLS", New Vector2(x + 2, currentY), LabelAccentColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, "<-> Move", New Vector2(x + 2, currentY), helpColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, "v   Soft Drop", New Vector2(x + 2, currentY), helpColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, "^   Rotate", New Vector2(x + 2, currentY), helpColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, "SPACE Hard Drop", New Vector2(x + 2, currentY), helpColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, "P   Pause", New Vector2(x + 2, currentY), helpColor)
        currentY += lineHeight
        spriteBatch.DrawString(font, "R   Restart", New Vector2(x + 2, currentY), helpColor)
    End Sub

    Private Sub DrawGameOverOverlay(boardX As Integer, boardY As Integer, boardWidth As Integer, boardHeight As Integer)
        ' Semi-transparent overlay
        spriteBatch.Draw(WhiteTexture, New Rectangle(boardX, boardY, boardWidth, boardHeight), New Color(0, 0, 0, 180))

        Dim font As SpriteFont = GetFont()
        If font Is Nothing Then Return

        Dim gameOverText As String = "GAME OVER"
        Dim textSize As Vector2 = font.MeasureString(gameOverText)
        Dim textPos As New Vector2(
            boardX + (boardWidth - textSize.X) / 2,
            boardY + (boardHeight - textSize.Y) / 2 - 12)
        spriteBatch.DrawString(font, gameOverText, textPos, Color.Red)

        Dim restartText As String = "Press R to restart"
        Dim restartSize As Vector2 = font.MeasureString(restartText)
        Dim restartPos As New Vector2(
            boardX + (boardWidth - restartSize.X) / 2,
            boardY + (boardHeight - restartSize.Y) / 2 + 12)
        spriteBatch.DrawString(font, restartText, restartPos, LabelColor)
    End Sub

    Private Sub DrawPauseOverlay(boardX As Integer, boardY As Integer, boardWidth As Integer, boardHeight As Integer)
        ' Semi-transparent overlay
        spriteBatch.Draw(WhiteTexture, New Rectangle(boardX, boardY, boardWidth, boardHeight), New Color(0, 0, 0, 150))

        Dim font As SpriteFont = GetFont()
        If font Is Nothing Then Return

        Dim pauseText As String = "PAUSED"
        Dim textSize As Vector2 = font.MeasureString(pauseText)
        Dim textPos As New Vector2(
            boardX + (boardWidth - textSize.X) / 2,
            boardY + (boardHeight - textSize.Y) / 2)
        spriteBatch.DrawString(font, pauseText, textPos, Color.Yellow)
    End Sub

#End Region

End Class
