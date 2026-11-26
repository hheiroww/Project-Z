Namespace [Shared].Physics
    ''' <summary>
    ''' A reproducible linear congruential sequence for small, seeded simulations.
    ''' Keep one mutable instance per stream. A default instance starts at seed zero.
    ''' </summary>
    Public Structure LinearCongruentialRandom
        Public Const Modulus As Integer = 134456
        Private Const Multiplier As Integer = 8121
        Private Const Increment As Integer = 28411
        Private currentState As Integer

        ''' <summary>Starts the sequence at the seed modulo <see cref="Modulus"/>.</summary>
        Public Sub New(seed As Long)
            Dim remainder = CInt(seed Mod Modulus)
            currentState = If(remainder < 0, remainder + Modulus, remainder)
        End Sub

        ''' <summary>The current state. Pass it to the constructor to resume the sequence.</summary>
        Public ReadOnly Property State As Integer
            Get
                Return currentState
            End Get
        End Property

        ''' <summary>Advances the sequence and returns its state in [0, Modulus).</summary>
        Public Function NextState() As Integer
            currentState = (Multiplier * currentState + Increment) Mod Modulus
            Return currentState
        End Function

        ''' <summary>Advances the sequence and returns a double-precision sample in [0, 1).</summary>
        Public Function NextDouble() As Double
            Return NextState() / CDbl(Modulus)
        End Function

        ''' <summary>Advances the sequence and returns a single-precision sample in [0, 1).</summary>
        Public Function NextSingle() As Single
            Return CSng(NextDouble())
        End Function

        ''' <summary>Returns the first normalized sample for a seed without retaining state.</summary>
        Public Shared Function Sample(seed As Long) As Double
            Dim sequence As New LinearCongruentialRandom(seed)
            Return sequence.NextDouble()
        End Function
    End Structure
End Namespace
