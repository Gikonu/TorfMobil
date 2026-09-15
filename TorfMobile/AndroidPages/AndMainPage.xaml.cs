
namespace TorfMobile
{
    public partial class AndMainPage : ContentPage
    {
        int count = 0;

        public AndMainPage()
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
