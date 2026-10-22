namespace TorfMobile.Network;

public static class NetworkSettings
{
    public const string Application = "TorfMobile";
    public const int ProtocolVersion = 1;
    public const int DiscoveryPort = 5001;
    public const int TcpPort = 5000;
    public const int MaxMessageBytes = 1024 * 1024; // Limit pojedynczej wiadomości TCP: 1 MiB

    public static readonly TimeSpan ScanDuration = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);
}
