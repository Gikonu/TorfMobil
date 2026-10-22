using Microsoft.Maui.Devices;

namespace TorfMobile.Network;

/// <summary>Jeden wspólny punkt dostępu do UDP i TCP dla MAUI.</summary>
public sealed class NetworkService
{
    public LanDevice LocalDevice { get; }
    public DeviceDiscoveryService Discovery { get; }
    public TcpConnectionManager Connections { get; }

    private bool _started;

    public NetworkService()
    {
        LocalDevice = DeviceIdentity.GetLocalDevice();
        Discovery = new DeviceDiscoveryService(LocalDevice);
        Connections = new TcpConnectionManager(LocalDevice);
    }

    public void Start()
    {
        if (_started)
            return;

        // Na Androidzie obsługujemy przychodzące UDP i TCP.
        // Windows inicjuje skan UDP i wychodzące TCP (bez reguł przychodzących).
        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        {
            Connections.StartListening();
            try
            {
                Discovery.StartResponder();
            }
            catch
            {
                _ = Connections.StopAsync();
                throw;
            }
        }

        _started = true;
    }

    public Task<IReadOnlyList<LanDevice>> ScanAsync(CancellationToken cancellationToken = default) =>
        Discovery.ScanAsync(cancellationToken: cancellationToken);

    public Task ConnectAsync(LanDevice device, CancellationToken cancellationToken = default) =>
        Connections.ConnectAsync(device, cancellationToken);

    public async Task StopAsync()
    {
        await Discovery.StopResponderAsync();
        await Connections.StopAsync();
        _started = false;
    }
}
