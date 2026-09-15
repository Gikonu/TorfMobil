
namespace TorfMobile
{
    public partial class WinMainPage : ContentPage
    {
        int count = 0;

        public WinMainPage()
        {
            InitializeComponent();
            
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            count++;

            CounterBtn.Text = $"Fuel used: {count}L";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}
