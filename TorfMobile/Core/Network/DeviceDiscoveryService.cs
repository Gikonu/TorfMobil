using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace TorfMobile.Network;

/// <summary>
/// UDP: skanowanie wielu urządzeń oraz odpowiedzi na skanowanie.
/// W rozwiązaniu bez reguły zapory na Windows PC skanuje, a Android odpowiada.
/// </summary>
public sealed class DeviceDiscoveryService
{
    private readonly LanDevice _local;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private UdpClient? _responder;
    private CancellationTokenSource? _responderCancellation;
    private Task? _responderTask;

    public event Action<Exception>? Error;

    public DeviceDiscoveryService(LanDevice local) => _local = local;

    /// <summary>Uruchom raz na urządzeniach przyjmujących discovery (np. Android).</summary>
    public void StartResponder()
    {
        if (_responder is not null)
            return;

        UdpClient udp = new UdpClient(new IPEndPoint(IPAddress.Any, NetworkSettings.DiscoveryPort));
        CancellationTokenSource cts = new();

        _responder = udp;
        _responderCancellation = cts;
        _responderTask = RespondLoopAsync(udp, cts.Token);
    }

    private async Task RespondLoopAsync(UdpClient udp, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UdpReceiveResult received = await udp.ReceiveAsync(cancellationToken);
                DiscoveryPacket? request = TryRead(received.Buffer);

                if (request is null || request.Type != "discover" || request.DeviceId == _local.Id)
                    continue;

                DiscoveryPacket reply = CreatePacket("here");
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(reply);
                await udp.SendAsync(payload, payload.Length, received.RemoteEndPoint);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { Error?.Invoke(ex); }
    }

    /// <summary>Wyślij broadcast i zbieraj wszystkie odpowiedzi przez określony czas.</summary>
    public async Task<IReadOnlyList<LanDevice>> ScanAsync(
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        await _scanLock.WaitAsync(cancellationToken);
        try
        {
            using UdpClient udp = new UdpClient(AddressFamily.InterNetwork);
            udp.EnableBroadcast = true;

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(duration ?? NetworkSettings.ScanDuration);

            DiscoveryPacket request = CreatePacket("discover");
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request);

            await udp.SendAsync(payload, payload.Length,
                new IPEndPoint(IPAddress.Broadcast, NetworkSettings.DiscoveryPort));

            Dictionary<string, LanDevice> devices = new(StringComparer.Ordinal);

            try
            {
                while (true)
                {
                    UdpReceiveResult received = await udp.ReceiveAsync(timeout.Token);
                    DiscoveryPacket? reply = TryRead(received.Buffer);

                    if (reply is null || reply.Type != "here" || reply.DeviceId == _local.Id)
                        continue;

                    if (string.IsNullOrWhiteSpace(reply.DeviceId) ||
                        reply.TcpPort is < 1 or > 65535)
                        continue;

                    // Adres IP pochodzi z pakietu, nie z deklaracji nadawcy.
                    devices[reply.DeviceId] = new LanDevice
                    {
                        Id = reply.DeviceId,
                        Name = reply.DeviceName,
                        Platform = reply.Platform,
                        IpAddress = received.RemoteEndPoint.Address.ToString(),
                        TcpPort = reply.TcpPort
                    };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Upłynął czas skanowania: zwracamy wszystkie znalezione urządzenia.
            }

            cancellationToken.ThrowIfCancellationRequested();
            return devices.Values.OrderBy(d => d.Name).ToList();
        }
        finally
        {
            _scanLock.Release();
        }
    }

    public async Task StopResponderAsync()
    {
        if (_responder is null)
            return;

        _responderCancellation!.Cancel();
        _responder.Dispose();

        if (_responderTask is not null)
            await _responderTask;

        _responderCancellation.Dispose();
        _responderCancellation = null;
        _responderTask = null;
        _responder = null;
    }

    private DiscoveryPacket CreatePacket(string type) => new()
    {
        Type = type,
        DeviceId = _local.Id,
        DeviceName = _local.Name,
        Platform = _local.Platform,
        TcpPort = _local.TcpPort
    };

    private static DiscoveryPacket? TryRead(byte[] bytes)
    {
        try
        {
            if (bytes.Length > 2048)
                return null;

            DiscoveryPacket? packet = JsonSerializer.Deserialize<DiscoveryPacket>(bytes);
            if (packet?.Application != NetworkSettings.Application ||
                packet.Version != NetworkSettings.ProtocolVersion)
                return null;

            return packet;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
