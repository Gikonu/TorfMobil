using System.Net.Sockets;

namespace TorfMobile.Network;

/// <summary>Jedno aktywne połączenie z konkretnym urządzeniem.</summary>
public sealed class PeerConnection
{
    private readonly TcpClient _client;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public LanDevice Device { get; }
    internal NetworkStream Stream => _client.GetStream();

    internal PeerConnection(LanDevice device, TcpClient client)
    {
        Device = device;
        _client = client;
    }

    internal async Task SendAsync(TcpMessage message, CancellationToken cancellationToken = default)
    {
        // Dwa równoległe WriteAsync nie mogą przeplatać nagłówków i danych.
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await TcpMessageProtocol.SendAsync(Stream, message, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    internal void Close() => _client.Dispose();
}
