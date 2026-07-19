using System.Diagnostics;

namespace TorfMobile
{
	public partial class FoilPage : ContentPage
	{
        FoilMagazine magazine;
        bool bIsSaving = false;

        public FoilPage(FoilMagazine magazine)
		{
			InitializeComponent();

            this.magazine = magazine;

            ProductPicker.ItemsSource = magazine.foilData;
            ProductPicker.ItemDisplayBinding = new Binding("Name");

            HistoryCollection.ItemsSource = magazine.foilData;
        }

        private void OnViewChange(object sender, EventArgs e)
        {
            
        }

        private async void OnAddCliced(object sender, EventArgs e)
        {
            if (QuantityEntry.Text != null)
            {
                if (bIsSaving || int.Parse(QuantityEntry.Text) == 0)
                    return;

                string text = AddButton.Text;
                AddButton.Text = "Dodane";
                bIsSaving = true;

                var t = (FoilMagazine.FoilData)ProductPicker.SelectedItem;
                if (t != null)
                    t.Amount += int.Parse(QuantityEntry.Text);

                await Task.Delay(2000);

                AddButton.Text = text;
                bIsSaving = false;
            }
        }
    }
}