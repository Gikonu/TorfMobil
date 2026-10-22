namespace TorfMobile.Network;

/// <summary>Ramka TCP: na razie hello oraz text; potem można dodać komunikaty synchronizacji.</summary>
public sealed class TcpMessage
{
    public string Application { get; set; } = NetworkSettings.Application;
    public int Version { get; set; } = NetworkSettings.ProtocolVersion;
    public string Type { get; set; } = ""; // hello / text
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Platform { get; set; } = "";
    public int TcpPort { get; set; } = NetworkSettings.TcpPort;
    public string Text { get; set; } = "";
}
