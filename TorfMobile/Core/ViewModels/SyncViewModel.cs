using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TorfMobile.Network;
using TorfMobile.Sync;

namespace TorfMobile.ViewModels;

public sealed class SyncViewModel : INotifyPropertyChanged
{
    private readonly NetworkService _network;
    private readonly FoilSyncService _sync;
    private LanDevice? _selectedPeer;
    private string _selectedMode = "Sumuj";
    private string _selectedSource = "Moje urządzenie";
    private string _status = "Wybierz urządzenie i rodzaj zliczenia.";
    private bool _isBusy;

    public ObservableCollection<LanDevice> ConnectedDevices { get; } = new();
    public ObservableCollection<SyncCategoryOption> Categories { get; } = new()
    {
        new SyncCategoryOption("foil", "Folia", true)
    };
    public IReadOnlyList<string> Modes { get; } = new[] { "Sumuj", "Nadpisz / przenieś" };
    public IReadOnlyList<string> Sources { get; } = new[] { "Moje urządzenie", "Drugie urządzenie" };

    public LanDevice? SelectedPeer
    {
        get => _selectedPeer;
        set { if (Set(ref _selectedPeer, value)) OnPropertyChanged(nameof(CanSync)); }
    }
    public string SelectedMode
    {
        get => _selectedMode;
        set { if (Set(ref _selectedMode, value)) { OnPropertyChanged(nameof(IsOverwrite)); OnPropertyChanged(nameof(ModeDescription)); } }
    }
    public bool IsOverwrite => SelectedMode == "Nadpisz / przenieś";
    public string ModeDescription => IsOverwrite
        ? "Na urządzeniu docelowym wybrane zliczenia zostaną zastąpione danymi ze źródła."
        : "Połącz zmiany z obu urządzeń i zapisz wspólną sumę. Ponowna synchronizacja nie dodaje tych samych zliczeń ponownie.";
    public string SelectedSource { get => _selectedSource; set => Set(ref _selectedSource, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(CanSync)); }
    }
    public bool CanSync => !IsBusy && SelectedPeer is not null;
    public ICommand SynchronizeCommand { get; }
    public ICommand RefreshPeersCommand { get; }
    public Func<string, Task<bool>>? ConfirmAsync { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SyncViewModel(NetworkService network, FoilSyncService sync, FoilMagazine magazine)
    {
        _network = network;
        _sync = sync;
        SynchronizeCommand = new Command(async () => await SynchronizeAsync());
        RefreshPeersCommand = new Command(RefreshPeers);
        _network.Connections.DeviceConnected += _ => MainThread.BeginInvokeOnMainThread(RefreshPeers);
        _network.Connections.DeviceDisconnected += _ => MainThread.BeginInvokeOnMainThread(RefreshPeers);
        _sync.Diagnostic += e => MainThread.BeginInvokeOnMainThread(() => Status = e);
        foreach (var category in Categories) category.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSync));
        RefreshPeers();
        _sync.FoilDataChanged += magazine.ReloadFromDatabase;

    }

    public void RefreshPeers()
    {
        var current = SelectedPeer?.Id;
        var peers = _network.Connections.ConnectedDevices;
        ConnectedDevices.Clear();
        foreach (var peer in peers.OrderBy(p => p.Name)) ConnectedDevices.Add(peer);
        SelectedPeer = ConnectedDevices.FirstOrDefault(p => p.Id == current) ?? ConnectedDevices.FirstOrDefault();
        if (ConnectedDevices.Count == 0) Status = "Brak połączonych urządzeń. Najpierw użyj zakładki Połączenia.";
    }

    private async Task SynchronizeAsync()
    {
        if (IsBusy || SelectedPeer is null) return;
        var selected = Categories.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0) { Status = "Zaznacz przynajmniej jedną kategorię."; return; }
        if (selected.Any(c => c.Key != "foil")) { Status = "Ta kategoria nie ma jeszcze adaptera bazy."; return; }
        if (!_network.Connections.IsConnected(SelectedPeer.Id)) { Status = "Urządzenie nie jest połączone."; RefreshPeers(); return; }

        bool sourceIsLocal = SelectedSource == "Moje urządzenie";
        string description = IsOverwrite
            ? $"FOLIA: nadpisać zliczenia na urządzeniu '{(sourceIsLocal ? SelectedPeer.Name : _network.LocalDevice.Name)}' danymi z '{(sourceIsLocal ? _network.LocalDevice.Name : SelectedPeer.Name)}'? Inne kategorie pozostaną bez zmian."
            : $"FOLIA: połączyć zliczenia z '{_network.LocalDevice.Name}' i '{SelectedPeer.Name}' oraz zapisać wynik na obu urządzeniach?";
        if (ConfirmAsync is null || !await ConfirmAsync(description)) return;

        IsBusy = true;
        Status = "Synchronizacja w toku. Potwierdź żądanie na drugim urządzeniu...";
        try
        {
            await _sync.SynchronizeAsync(SelectedPeer, IsOverwrite ? "overwrite" : "sum", sourceIsLocal);
            Status = "Synchronizacja zakończona. Wybrane zliczenia Folia zostały zapisane.";
        }
        catch (Exception ex) { Status = $"Synchronizacja nieudana: {ex.Message}"; }
        finally 
        { 
            IsBusy = false; 
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(property); return true;
    }
    private void OnPropertyChanged(string? property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
