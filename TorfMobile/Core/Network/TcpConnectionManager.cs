using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace TorfMobile.Network;

/// <summary>
/// Zarządza wieloma połączeniami TCP, zarówno wychodzącymi, jak i przychodzącymi.
/// Listener uruchamiaj na telefonie. Windows może pozostać wyłącznie klientem.
/// </summary>
public sealed class TcpConnectionManager
{
    private readonly LanDevice _local;
    private readonly ConcurrentDictionary<string, PeerConnection> _peers = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _acceptTask;

    public TcpConnectionManager(LanDevice local) => _local = local;

    public event Action<LanDevice>? DeviceConnected;
    public event Action<LanDevice>? DeviceDisconnected;
    public event Action<LanDevice, string>? TextReceived;
    public event Action<LanDevice, TcpMessage>? MessageReceived;
    public event Action<Exception>? Error;

    public IReadOnlyList<LanDevice> ConnectedDevices =>
        _peers.Values.Select(peer => peer.Device).ToList();

    public bool IsConnected(string deviceId) => _peers.ContainsKey(deviceId);

    /// <summary>Uruchom listener na Androidzie, aby móc przyjmować wiele połączeń TCP.</summary>
    public void StartListening()
    {
        if (_listener is not null)
            return;

        TcpListener listener = new TcpListener(IPAddress.Any, NetworkSettings.TcpPort);
        listener.Start();

        CancellationTokenSource cts = new();
        _listener = listener;
        _listenerCancellation = cts;
        _acceptTask = AcceptLoopAsync(listener, cts.Token);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);

                // Osobna obsługa każdego klienta: kolejne połączenia nie czekają.
                _ = HandleIncomingAsync(client);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { Error?.Invoke(ex); }
    }

    /// <summary>Połącz się z wybranym urządzeniem z wyniku UDP.</summary>
    public async Task ConnectAsync(LanDevice target, CancellationToken cancellationToken = default)
    {
        if (target.Id == _local.Id)
            throw new InvalidOperationException("Nie można połączyć się z samym sobą.");

        if (IsConnected(target.Id))
            return;

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(NetworkSettings.ConnectTimeout);

        TcpClient client = new TcpClient(AddressFamily.InterNetwork);
        bool accepted = false;

        try
        {
            await client.ConnectAsync(IPAddress.Parse(target.IpAddress), target.TcpPort, timeout.Token);
            NetworkStream stream = client.GetStream();

            await TcpMessageProtocol.SendAsync(stream, CreateHello(), timeout.Token);
            TcpMessage reply = await TcpMessageProtocol.ReceiveAsync(stream, timeout.Token);

            if (!IsValidHello(reply) || reply.DeviceId != target.Id)
                throw new InvalidDataException("Odpowiedź TCP nie pochodzi od oczekiwanego urządzenia.");

            PeerConnection peer = new PeerConnection(new LanDevice
            {
                Id = reply.DeviceId,
                Name = reply.DeviceName,
                Platform = reply.Platform,
                IpAddress = target.IpAddress,
                TcpPort = target.TcpPort
            }, client);

            if (!_peers.TryAdd(peer.Device.Id, peer))
                return; // Ktoś już nawiązał to połączenie równolegle.

            accepted = true;
            DeviceConnected?.Invoke(peer.Device);
            _ = WatchPeerAsync(peer);
        }
        finally
        {
            if (!accepted)
                client.Dispose();
        }
    }

    private async Task HandleIncomingAsync(TcpClient client)
    {
        bool accepted = false;
        try
        {
            using CancellationTokenSource timeout = new(NetworkSettings.ConnectTimeout);
            NetworkStream stream = client.GetStream();

            TcpMessage hello = await TcpMessageProtocol.ReceiveAsync(stream, timeout.Token);
            if (!IsValidHello(hello))
                throw new InvalidDataException("Nieprawidłowe przywitanie TCP.");

            // Potwierdzamy identyfikator własnego urządzenia.
            await TcpMessageProtocol.SendAsync(stream, CreateHello(), timeout.Token);

            string ip = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "";
            PeerConnection peer = new(new LanDevice
            {
                Id = hello.DeviceId,
                Name = hello.DeviceName,
                Platform = hello.Platform,
                IpAddress = ip,
                TcpPort = hello.TcpPort
            }, client);

            if (!_peers.TryAdd(peer.Device.Id, peer))
                return;

            accepted = true;
            DeviceConnected?.Invoke(peer.Device);
            _ = WatchPeerAsync(peer);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException
                                  or InvalidDataException or ObjectDisposedException)
        {
            // Nieudany handshake: zamykamy klienta, bez kończenia listenera.
            Error?.Invoke(ex);
        }
        finally
        {
            if (!accepted)
                client.Dispose();
        }
    }

    private async Task WatchPeerAsync(PeerConnection peer)
    {
        try
        {
            while (true)
            {
                TcpMessage message = await TcpMessageProtocol.ReceiveAsync(peer.Stream);

                if (message.DeviceId != peer.Device.Id)
                    throw new InvalidDataException("Nieprawidłowy identyfikator nadawcy.");

                MessageReceived?.Invoke(peer.Device, message);
                if (message.Type == "text")
                    TextReceived?.Invoke(peer.Device, message.Text);
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or SocketException
                                  or ObjectDisposedException)
        {
            // Normalne rozłączenie (lub utrata sieci).
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
        finally
        {
            RemovePeer(peer);
        }
    }

    public Task SendTextAsync(string deviceId, string text, CancellationToken cancellationToken = default)
    {
        return SendMessageAsync(deviceId, "text", text, cancellationToken);
    }

    /// <summary>Podstawa przyszłych komunikatów synchronizacji danych.</summary>
    public Task SendMessageAsync(string deviceId, string type, string text,
        CancellationToken cancellationToken = default)
    {
        if (!_peers.TryGetValue(deviceId, out PeerConnection? peer))
            throw new InvalidOperationException("Urządzenie nie jest połączone.");

        return peer.SendAsync(new TcpMessage
        {
            Type = type,
            DeviceId = _local.Id,
            DeviceName = _local.Name,
            Platform = _local.Platform,
            TcpPort = _local.TcpPort,
            Text = text
        }, cancellationToken);
    }

    public void Disconnect(string deviceId)
    {
        if (_peers.TryGetValue(deviceId, out PeerConnection? peer))
            RemovePeer(peer);
    }

    private void RemovePeer(PeerConnection peer)
    {
        // Usuwamy wyłącznie tę sesję, nie nową sesję o tym samym identyfikatorze.
        bool removed = ((ICollection<KeyValuePair<string, PeerConnection>>)_peers)
            .Remove(new KeyValuePair<string, PeerConnection>(peer.Device.Id, peer));

        peer.Close();
        if (removed)
            DeviceDisconnected?.Invoke(peer.Device);
    }

    public async Task StopAsync()
    {
        if (_listener is not null)
        {
            _listenerCancellation!.Cancel();
            _listener.Stop();

            if (_acceptTask is not null)
                await _acceptTask;

            _listenerCancellation.Dispose();
            _listenerCancellation = null;
            _listener = null;
            _acceptTask = null;
        }

        foreach (PeerConnection peer in _peers.Values.ToArray())
            RemovePeer(peer);
    }

    private TcpMessage CreateHello() => new()
    {
        Type = "hello",
        DeviceId = _local.Id,
        DeviceName = _local.Name,
        Platform = _local.Platform,
        TcpPort = _local.TcpPort
    };

    private bool IsValidHello(TcpMessage message) =>
        message.Type == "hello" &&
        !string.IsNullOrWhiteSpace(message.DeviceId) &&
        message.DeviceId != _local.Id &&
        message.TcpPort is > 0 and <= 65535;
}
