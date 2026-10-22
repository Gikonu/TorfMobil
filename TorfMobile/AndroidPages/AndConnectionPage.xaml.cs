using System.Collections.Specialized;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TorfMobile.Network;
using TorfMobile.ViewModels;

namespace TorfMobile;

public partial class AndConnectionPage : ContentPage
{
    private readonly DeviceConnectionViewModel _viewModel;
    private readonly NetworkService _network;

    public AndConnectionPage(DeviceConnectionViewModel viewModel, NetworkService network)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _network = network;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _network.Connections.DeviceConnected += OnDeviceChanged;
        _network.Connections.DeviceDisconnected += OnDeviceChanged;
        _viewModel.Devices.CollectionChanged += OnDevicesChanged;

        // Na wypadek gdy TCP zestawiło się przed otwarciem strony.
        _viewModel.RefreshFromNetwork();
        UpdateDiagnostics();
    }

    protected override void OnDisappearing()
    {
        _network.Connections.DeviceConnected -= OnDeviceChanged;
        _network.Connections.DeviceDisconnected -= OnDeviceChanged;
        _viewModel.Devices.CollectionChanged -= OnDevicesChanged;

        base.OnDisappearing();
    }

    private void OnDeviceChanged(LanDevice _) => UpdateDiagnostics();

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateDiagnostics();

    private void UpdateDiagnostics()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var tcp = _network.Connections.ConnectedDevices;

            NetworkDebugLabel.Text =
                $"Aktywne TCP: {tcp.Count} | Lista Devices: {_viewModel.Devices.Count} | " +
                $"VM połączone: {_viewModel.ConnectedDevices.Count}" +
                (tcp.Count > 0 ? $"\nTCP: {tcp[0].Name}, IP: {tcp[0].IpAddress}" : "");
        });
    }
}
