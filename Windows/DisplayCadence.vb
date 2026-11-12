Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing

Namespace Windows
    ''' <summary>Configures the frame cadence of a MonoGame window hosted by Project-Z.</summary>
    Public NotInheritable Class DisplayCadence
        Private Shared ReadOnly tearingSupport As New Lazy(Of Boolean)(AddressOf QueryTearingSupport)

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Sets the next device configuration and the target frame interval without resetting the device.
        ''' </summary>
        ''' <remarks>
        ''' Call before creating the graphics device. At runtime the host owns device resets and should
        ''' call GraphicsDeviceManager.ApplyChanges on its graphics thread at a suitable boundary.
        ''' MonoGame uses TargetElapsedTime for a fixed-step game. A variable-step host must use
        ''' the interval in its own frame pacing.
        ''' </remarks>
        Public Shared Sub Apply(game As Game, graphics As GraphicsDeviceManager, verticalSync As Boolean, targetFramesPerSecond As Integer)
            ArgumentNullException.ThrowIfNull(game)
            ArgumentNullException.ThrowIfNull(graphics)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetFramesPerSecond)

            game.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / targetFramesPerSecond)
            graphics.SynchronizeWithVerticalRetrace = verticalSync
        End Sub

        ''' <summary>
        ''' Reports whether the Windows DX12 backend can submit a tearing-enabled windowed DXGI flip.
        ''' </summary>
        ''' <remarks>
        ''' Requires V-sync off at presentation. Actual variable refresh depends on the display,
        ''' driver, and presentation path. Layered windows that present through DWM do not use
        ''' the MonoGame swap chain to display their content.
        ''' </remarks>
        Public Shared ReadOnly Property DxgiTearingSupported As Boolean
            Get
                Return tearingSupport.Value
            End Get
        End Property

        Private Shared Function QueryTearingSupport() As Boolean
            If Not OperatingSystem.IsWindowsVersionAtLeast(10) OrElse Not GraphicsBackendInfo.SupportsDirectX12 Then Return False

            Dim factory1 As IntPtr
            Dim factory5 As IntPtr
            Dim factory1Id As New Guid("770aae78-f26f-4dba-a829-253c83d1b387")
            Dim factory5Id As New Guid("7632e1f5-ee65-4dca-87fd-84cd75f8838d")
            Try
                If CreateDXGIFactory1(factory1Id, factory1) < 0 OrElse factory1 = IntPtr.Zero Then Return False
                If Marshal.QueryInterface(factory1, factory5Id, factory5) < 0 OrElse factory5 = IntPtr.Zero Then Return False

                ' IDXGIFactory5::CheckFeatureSupport follows its 28 inherited vtable entries.
                Dim checkAddress = Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory5), 28 * IntPtr.Size)
                Dim check = Marshal.GetDelegateForFunctionPointer(Of CheckFeatureSupport)(checkAddress)
                Const presentAllowTearing As Integer = 0
                Dim supported As Integer
                Return check(factory5, presentAllowTearing, supported, Marshal.SizeOf(Of Integer)()) >= 0 AndAlso supported <> 0
            Finally
                If factory5 <> IntPtr.Zero Then Marshal.Release(factory5)
                If factory1 <> IntPtr.Zero Then Marshal.Release(factory1)
            End Try
        End Function

        <DllImport("dxgi.dll", ExactSpelling:=True)>
        Private Shared Function CreateDXGIFactory1(ByRef interfaceId As Guid, ByRef factory As IntPtr) As Integer
        End Function

        <UnmanagedFunctionPointer(CallingConvention.StdCall)>
        Private Delegate Function CheckFeatureSupport(factory As IntPtr, feature As Integer, ByRef supported As Integer, size As Integer) As Integer
    End Class
End Namespace
