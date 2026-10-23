Imports System.IO
Imports System.Text.Json
Imports Microsoft.Xna.Framework

Namespace Windows.Composition
    ''' <summary>Background-only appearance shared by every window in an application.</summary>
    Public NotInheritable Class WindowAppearanceSettings
        Public Property DwmBlurEnabled As Boolean = True
        Private opacity As Integer = 50
        Public Property BackgroundOpacity As Integer
            Get
                Return opacity
            End Get
            Set(value As Integer)
                opacity = Math.Clamp(value, 0, 100)
            End Set
        End Property
        Public Function Copy() As WindowAppearanceSettings
            Return New WindowAppearanceSettings With {.DwmBlurEnabled = DwmBlurEnabled, .BackgroundOpacity = BackgroundOpacity}
        End Function
        Public Function Background(color As Color) As Color
            Return color * (BackgroundOpacity / 100.0F)
        End Function
        Public Sub Save(path As String)
            Dim parent = IO.Path.GetDirectoryName(IO.Path.GetFullPath(path))
            Directory.CreateDirectory(parent)
            File.WriteAllText(path, JsonSerializer.Serialize(Me))
        End Sub
        Public Shared Function Load(path As String) As WindowAppearanceSettings
            Return If(JsonSerializer.Deserialize(Of WindowAppearanceSettings)(File.ReadAllText(path)), New WindowAppearanceSettings())
        End Function
    End Class

    ''' <summary>One appearance controller per application, with live window bindings.</summary>
    Public NotInheritable Class WindowAppearance
        Public Shared ReadOnly Property [Default] As New WindowAppearance()
        Private value As New WindowAppearanceSettings()
        Public Event Changed As EventHandler
        Public ReadOnly Property Settings As WindowAppearanceSettings
            Get
                Return value.Copy()
            End Get
        End Property
        Public Sub Update(settings As WindowAppearanceSettings)
            ArgumentNullException.ThrowIfNull(settings)
            If value.BackgroundOpacity = settings.BackgroundOpacity AndAlso value.DwmBlurEnabled = settings.DwmBlurEnabled Then Return
            value = settings.Copy()
            RaiseEvent Changed(Me, EventArgs.Empty)
        End Sub
        ''' <summary>Call on the window's UI thread. Per-pixel hosts provide their own alpha presenter.</summary>
        Public Function Bind(hwnd As IntPtr, Optional backgroundChanged As Action(Of WindowAppearanceSettings) = Nothing,
                             Optional resizable As Boolean = True, Optional perPixelPresentation As Boolean = False) As WindowAppearanceBinding
            Return New WindowAppearanceBinding(Me, hwnd, backgroundChanged, resizable, perPixelPresentation)
        End Function
    End Class

    Public NotInheritable Class WindowAppearanceBinding
        Implements IDisposable
        Private ReadOnly appearance As WindowAppearance
        Private ReadOnly owner As IntPtr
        Private ReadOnly changed As Action(Of WindowAppearanceSettings)
        Private ReadOnly resizable As Boolean
        Private ReadOnly perPixel As Boolean
        Private ReadOnly chrome As BorderlessWindowChrome
        Private disposed As Boolean
        Public ReadOnly Property Backdrop As DwmBackdropHost
        Public ReadOnly Property Composition As WindowCompositionResult
        Public ReadOnly Property BackdropComposition As WindowCompositionResult

        Friend Sub New(appearance As WindowAppearance, hwnd As IntPtr, callback As Action(Of WindowAppearanceSettings), canResize As Boolean, perPixelPresentation As Boolean)
            Me.appearance = appearance : owner = hwnd : changed = callback : resizable = canResize : perPixel = perPixelPresentation
            chrome = New BorderlessWindowChrome(hwnd, canResize)
            If perPixel Then Backdrop = New DwmBackdropHost(hwnd, Color.Transparent)
            AddHandler appearance.Changed, AddressOf Apply
            Apply(Me, EventArgs.Empty)
        End Sub

        Private Sub Apply(sender As Object, args As EventArgs)
            If disposed Then Return
            Dim settings = appearance.Settings
            Dim material = If(settings.DwmBlurEnabled, WindowBackdrop.Acrylic, WindowBackdrop.None)
            _Composition = WindowsComposition.ApplyToHandle(owner, New WindowCompositionOptions With {
                .Borderless = True, .Resizable = resizable, .Backdrop = If(perPixel, WindowBackdrop.None, material),
                .FallbackAccentBlur = Not perPixel AndAlso settings.DwmBlurEnabled,
                .Tint = Color.Transparent, .ExtendFrameIntoClientArea = Not perPixel AndAlso settings.DwmBlurEnabled,
                .Opacity = If(perPixel, CByte(0), Byte.MaxValue)})
            If Backdrop IsNot Nothing Then
                _BackdropComposition = WindowsComposition.ApplyToHandle(Backdrop.Handle, New WindowCompositionOptions With {
                    .Borderless = True, .Resizable = False, .Backdrop = material, .Tint = Color.Transparent,
                    .FallbackAccentBlur = settings.DwmBlurEnabled, .ExtendFrameIntoClientArea = settings.DwmBlurEnabled,
                    .Opacity = If(settings.DwmBlurEnabled, Byte.MaxValue, CByte(0))})
                Backdrop.Tick()
            End If
            changed?.Invoke(settings)
        End Sub

        Public Sub Tick()
            If Not disposed Then Backdrop?.Tick()
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            RemoveHandler appearance.Changed, AddressOf Apply
            Backdrop?.Dispose()
            chrome.Dispose()
        End Sub
    End Class
End Namespace
