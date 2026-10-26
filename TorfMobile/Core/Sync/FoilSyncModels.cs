using System.ComponentModel;

namespace TorfMobile.Sync;

// Zostawiamy zgodność z istniejącym SyncViewModel i obydwoma stronami XAML.
public sealed class SyncCategoryOption : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Key { get; }
    public string Name { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public SyncCategoryOption(string key, string name, bool selected = false)
        => (Key, Name, _isSelected) = (key, name, selected);

    public event PropertyChangedEventHandler? PropertyChanged;
}

// Jeden FoilItem = JEDEN wiersz tabeli Foil = JEDNA rolka/zliczenie.
// Index to Foil.ProductCode (nie unikalny identyfikator produktu).
// GlobalId pozwala rozpoznać ten sam wiersz na różnych urządzeniach;
// lokalne Foil.Id NIE jest identyfikatorem globalnym.
public sealed class FoilItem
{
    public string GlobalId { get; set; } = "";
    public string Index { get; set; } = "";
    public string Name { get; set; } = "";
    public long Amount { get; set; }
}

public sealed class FoilSnapshot
{
    public string Epoch { get; set; } = "initial";
    public List<FoilItem> Items { get; set; } = new();
}

// API komunikacji pozostaje takie samo jak w SyncViewModel/FoilSyncService v1.
public sealed class SyncEnvelope
{
    public string Kind { get; set; } = "";
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string ReplyTo { get; set; } = "";
    public string OperationId { get; set; } = "";
    public string Category { get; set; } = "foil";
    public string Mode { get; set; } = "sum";
    public string SourceDeviceId { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string TargetName { get; set; } = "";
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public string ExpectedHash { get; set; } = "";
    public FoilSnapshot? Snapshot { get; set; }
}
