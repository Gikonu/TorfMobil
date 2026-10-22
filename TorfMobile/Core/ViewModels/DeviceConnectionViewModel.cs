using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TorfMobile.Network;

namespace TorfMobile.ViewModels;

/// <summary>
/// Wspólny ViewModel dla Windows i Androida.
/// Devices = suma urządzeń odkrytych przez UDP i połączonych przez TCP.
/// Dzięki temu PC pojawia się na telefonie po handshake TCP,
/// choć Android nie może odnaleźć PC przez broadcast UDP.
/// </summary>
public sealed class DeviceConnectionViewModel : INotifyPropertyChanged
{
    private readonly NetworkService _network;
    private readonly Dictionary<string, LanDevice> _udpDevices = new(StringComparer.Ordinal);
    private LanDevice? _selectedDevice;
    private bool _isScanning;
    private string _scanStatus = "Gotowe do skanowania";
    private string _connectionStatus = "Wybierz urządzenie";
    private string _connectionDetails = "";

    public ObservableCollection<LanDevice> Devices { get; } = new();
    public ObservableCollection<LanDevice> ConnectedDevices { get; } = new();

    public ICommand ScanCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand SendTestCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceConnectionViewModel(NetworkService network)
    {
        _network = network;

        ScanCommand = new Command(async () => await ScanAsync());
        ConnectCommand = new Command(async () => await ConnectAsync());
        DisconnectCommand = new Command(Disconnect);
        SendTestCommand = new Command(async () => await SendTestAsync());

        _network.Connections.DeviceConnected += OnDeviceConnected;
        _network.Connections.DeviceDisconnected += OnDeviceDisconnected;
        _network.Connections.TextReceived += OnTextReceived;
        _network.Connections.Error += OnNetworkError;
        _network.Discovery.Error += OnNetworkError;

        try
        {
            // Android uruchamia nasłuch TCP/UDP; Windows inicjuje połączenia.
            _network.Start();
        }
        catch (Exception ex)
        {
            ScanStatus = $"Błąd uruchomienia: {ex.Message}";
        }

        // Odtwórz także sesje TCP ustanowione zanim otwarto tę stronę.
        RefreshDeviceLists();
    }

    public LanDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetField(ref _selectedDevice, value))
                UpdateConnectionStatus();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set => SetField(ref _isScanning, value);
    }

    public string ScanStatus
    {
        get => _scanStatus;
        private set => SetField(ref _scanStatus, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetField(ref _connectionStatus, value);
    }

    public string ConnectionDetails
    {
        get => _connectionDetails;
        private set => SetField(ref _connectionDetails, value);
    }

    // Uzgodnij listę z już aktywnymi połączeniami, także po otwarciu strony.
    public void RefreshFromNetwork()
    {
        if (MainThread.IsMainThread)
            RefreshDeviceLists();
        else
            MainThread.BeginInvokeOnMainThread(RefreshDeviceLists);
    }

    public async Task ScanAsync()
    {
        if (IsScanning)
            return;

        IsScanning = true;
        ScanStatus = "Skanowanie sieci lokalnej...";

        try
        {
            IReadOnlyList<LanDevice> found = await _network.ScanAsync();

            _udpDevices.Clear();
            foreach (LanDevice device in found)
                _udpDevices[device.Id] = device;

            // Nie czyścimy Devices! Zachowujemy urządzenia połączone przez TCP.
            RefreshDeviceLists();
            ScanStatus = $"Widoczne urządzenia: {Devices.Count} (TCP: {ConnectedDevices.Count})";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Błąd skanowania: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task ConnectAsync()
    {
        LanDevice? device = SelectedDevice;
        if (device is null)
        {
            ConnectionStatus = "Najpierw wybierz urządzenie";
            return;
        }

        if (_network.Connections.IsConnected(device.Id))
        {
            UpdateConnectionStatus();
            return;
        }

        try
        {
            ConnectionStatus = "Łączenie...";
            await _network.ConnectAsync(device);
            RefreshDeviceLists();
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Błąd połączenia: {ex.Message}";
        }
    }

    public void Disconnect()
    {
        if (SelectedDevice is null)
            return;

        _network.Connections.Disconnect(SelectedDevice.Id);
        RefreshDeviceLists();
    }

    public async Task SendTestAsync()
    {
        if (SelectedDevice is null)
            return;

        try
        {
            await _network.Connections.SendTextAsync(
                SelectedDevice.Id,
                $"Test z {_network.LocalDevice.Name}");
            ConnectionDetails = "Wysłano wiadomość testową";
        }
        catch (Exception ex)
        {
            ConnectionDetails = $"Błąd wysyłania: {ex.Message}";
        }
    }

    private void OnDeviceConnected(LanDevice device) => MainThread.BeginInvokeOnMainThread(() =>
    {
        // Telefon zna teraz PC na podstawie wiadomości TCP 'hello'.
        // Nie jest potrzebna odpowiedź Windows na UDP.
        RefreshDeviceLists();
        ScanStatus = $"Widoczne urządzenia: {Devices.Count} (TCP: {ConnectedDevices.Count})";
    });

    private void OnDeviceDisconnected(LanDevice device) => MainThread.BeginInvokeOnMainThread(() =>
    {
        RefreshDeviceLists();
        ScanStatus = $"Widoczne urządzenia: {Devices.Count} (TCP: {ConnectedDevices.Count})";
    });

    private void OnTextReceived(LanDevice device, string text) => MainThread.BeginInvokeOnMainThread(() =>
    {
        ConnectionDetails = $"{device.Name}: {text}";
    });

    private void OnNetworkError(Exception ex) => MainThread.BeginInvokeOnMainThread(() =>
    {
        ConnectionDetails = $"Sieć: {ex.Message}";
    });

    /// <summary>
    /// Uzgadnia kolekcję widoczną w XAML z dwoma źródłami:
    /// UDP (odkryte urządzenia) oraz TCP (aktualnie połączone urządzenia).
    /// Tę metodę wywołujemy z wątku UI MAUI.
    /// </summary>
    private void RefreshDeviceLists()
    {
        IReadOnlyList<LanDevice> connected = _network.Connections.ConnectedDevices;
        HashSet<string> connectedIds = connected.Select(d => d.Id)
            .ToHashSet(StringComparer.Ordinal);

        var all = new Dictionary<string, LanDevice>(_udpDevices, StringComparer.Ordinal);
        foreach (LanDevice device in connected)
            all[device.Id] = device;

        // Usuwamy tylko urządzenia, które nie są ani odkryte, ani połączone.
        foreach (LanDevice previous in Devices.ToArray())
        {
            if (!all.ContainsKey(previous.Id))
                Devices.Remove(previous);
        }

        foreach (LanDevice device in all.Values.OrderBy(d => d.Name))
        {
            LanDevice? existing = Devices.FirstOrDefault(d => d.Id == device.Id);
            if (existing is null)
            {
                existing = new LanDevice { Id = device.Id };
                Devices.Add(existing);
            }

            // Nadpisujemy aktualną nazwę, platformę i adres IP.
            existing.Name = device.Name;
            existing.Platform = device.Platform;
            existing.IpAddress = device.IpAddress;
            existing.TcpPort = device.TcpPort;
            existing.IsConnected = connectedIds.Contains(device.Id);
        }

        // Kolekcja opcjonalna, przydatna np. dla ekranu aktywnych sesji.
        foreach (LanDevice previous in ConnectedDevices.ToArray())
        {
            if (!connectedIds.Contains(previous.Id))
                ConnectedDevices.Remove(previous);
        }
        foreach (LanDevice item in Devices.Where(d => d.IsConnected))
        {
            if (!ConnectedDevices.Any(d => d.Id == item.Id))
                ConnectedDevices.Add(item);
        }

        // Utrzymujemy referencję do obiektu z ItemsSource.
        if (SelectedDevice is not null)
        {
            LanDevice? stillVisible = Devices.FirstOrDefault(d => d.Id == SelectedDevice.Id);
            if (stillVisible is null)
                SelectedDevice = null;
            else if (!ReferenceEquals(SelectedDevice, stillVisible))
                SelectedDevice = stillVisible;
        }
        else if (connected.Count > 0)
        {
            // Na telefonie automatycznie pokaż stan pierwszego połączenia TCP.
            SelectedDevice = Devices.FirstOrDefault(d => d.IsConnected);
        }

        UpdateConnectionStatus();
    }

    private void UpdateConnectionStatus()
    {
        if (SelectedDevice is null)
        {
            ConnectionStatus = "Wybierz urządzenie";
            ConnectionDetails = $"Połączonych urządzeń: {ConnectedDevices.Count}";
            return;
        }

        ConnectionStatus = _network.Connections.IsConnected(SelectedDevice.Id)
            ? "Połączono"
            : "Rozłączono";

        ConnectionDetails = $"{SelectedDevice.IpAddress}:{SelectedDevice.TcpPort} • " +
                            $"Łącznie połączonych: {ConnectedDevices.Count}";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
