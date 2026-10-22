namespace TorfMobile.Network;

/// <summary>Mała wiadomość JSON wysyłana wyłącznie przez UDP.</summary>
public sealed class DiscoveryPacket
{
    public string Application { get; set; } = NetworkSettings.Application;
    public int Version { get; set; } = NetworkSettings.ProtocolVersion;
    public string Type { get; set; } = ""; // discover / here
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Platform { get; set; } = "";
    public int TcpPort { get; set; } = NetworkSettings.TcpPort;
}
