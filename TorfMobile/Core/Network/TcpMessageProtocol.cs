using System.Buffers.Binary;
using System.Text.Json;

namespace TorfMobile.Network;

/// <summary>
/// TCP jest strumieniem bajtów: dodajemy 4 bajty długości przed każdym JSON-em.
/// Dzięki temu ReadAsync nie pomyli dwóch wiadomości z jedną.
/// </summary>
public static class TcpMessageProtocol
{
    public static async Task SendAsync(Stream stream, TcpMessage message,
        CancellationToken cancellationToken = default)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);
        if (body.Length is < 1 or > NetworkSettings.MaxMessageBytes)
            throw new InvalidDataException("Wiadomość TCP jest zbyt duża.");

        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, body.Length);

        await stream.WriteAsync(header.AsMemory(), cancellationToken);
        await stream.WriteAsync(body.AsMemory(), cancellationToken);
    }

    public static async Task<TcpMessage> ReceiveAsync(Stream stream,
        CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header.AsMemory(), cancellationToken);

        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 1 or > NetworkSettings.MaxMessageBytes)
            throw new InvalidDataException("Nieprawidłowy rozmiar ramki TCP.");

        byte[] data = new byte[length];
        await stream.ReadExactlyAsync(data.AsMemory(), cancellationToken);

        TcpMessage? message = JsonSerializer.Deserialize<TcpMessage>(data);
        if (message is null ||
            message.Application != NetworkSettings.Application ||
            message.Version != NetworkSettings.ProtocolVersion)
            throw new InvalidDataException("Nieobsługiwany protokół TCP.");

        return message;
    }
}
