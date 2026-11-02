Namespace [Shared].Drawing.UI.Input
    ''' <summary>Application-owned decoder/audio backend. Call MediaElement.PresentFrame and
    ''' SignalOpened/SignalEnded/SignalFailed on the game thread; the application owns its textures.</summary>
    Public Interface IMediaPlaybackController
        Sub Open(source As String, decodePixelSize As Integer)
        Sub Play()
        Sub Pause()
        Sub [Stop]()
        Sub Seek(position As TimeSpan)
        Sub SetVolume(volume As Double)
    End Interface
End Namespace
