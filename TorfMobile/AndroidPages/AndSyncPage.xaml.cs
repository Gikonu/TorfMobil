using TorfMobile.ViewModels;

namespace TorfMobile;

public partial class AndSyncPage : ContentPage
{
    private readonly SyncViewModel _viewModel;

    public AndSyncPage(SyncViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Ta sama usługa i ViewModel są używane przez oba warianty UI.
        _viewModel.ConfirmAsync = message => DisplayAlert(
            "Potwierdź synchronizację", message, "Synchronizuj", "Anuluj");
        _viewModel.RefreshPeers();
    }
}
