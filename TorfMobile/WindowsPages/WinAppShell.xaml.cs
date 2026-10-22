
namespace TorfMobile
{
    public partial class WinAppShell : Shell
    {
        public WinAppShell()
        {
            InitializeComponent();

            SetActiveItem(HomeItem);
        }


        private async void HomeClicked(object sender, TappedEventArgs e)
        {
            SetActiveItem(HomeItem);

            await GoToAsync("//WinMainPage");
        }


        private async void FoilClicked(object sender, TappedEventArgs e)
        {
            SetActiveItem(FoilItem);

            await GoToAsync("//WinFoilPage");
        }

        private async void ConnectClicked(object sender, TappedEventArgs e)
        {
            SetActiveItem(ConnectItem);

            await GoToAsync("//WinConnectionPage");
        }

        //private async void SettingsClicked(object sender, TappedEventArgs e)
        //{
        //SetActiveItem(SettingsItem);

        //await GoToAsync("//WinSettingsPage");
        //}


        private void SetActiveItem(Border activeItem)
        {
            
            HomeItem.BackgroundColor = Colors.Transparent;
            FoilItem.BackgroundColor = Colors.Transparent;
            ConnectItem.BackgroundColor = Colors.Transparent;
            SettingsItem.BackgroundColor = Colors.Transparent;

            
            activeItem.BackgroundColor = Color.FromArgb("#3A3A3A");
        }
    }
}
