using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TorfMobile.Network;

/// <summary>
/// Informacje o urządzeniu. Własność IsConnected jest używana przez widok
/// do pokazywania stanu TCP niezależnie od tego, czy urządzenie odpowiada na UDP.
/// </summary>
public sealed class LanDevice : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _platform = "";
    private string _ipAddress = "";
    private int _tcpPort = NetworkSettings.TcpPort;
    private bool _isConnected;

    public string Id { get => _id; set => SetField(ref _id, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Platform { get => _platform; set => SetField(ref _platform, value); }
    public string IpAddress { get => _ipAddress; set => SetField(ref _ipAddress, value); }
    public int TcpPort { get => _tcpPort; set => SetField(ref _tcpPort, value); }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetField(ref _isConnected, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectionStatus)));
        }
    }

    public string ConnectionStatus => IsConnected ? "Połączono przez TCP" : "Dostępny przez UDP";

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    public override string ToString() => $"{Name} ({IpAddress}:{TcpPort})";
}
