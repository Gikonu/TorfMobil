
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace TorfMobile
{
	public partial class AndFoilPage : ContentPage
	{
        FoilMagazine magazine;
        bool bIsSaving = false;

        public AndFoilPage(FoilMagazine magazine)
		{
			InitializeComponent();

            this.magazine = magazine;

            ProductPicker.ItemsSource = magazine.foilData.Values.ToList<FoilData>();
            ProductPicker.ItemDisplayBinding = new Binding("Name");

            HistoryCollection.ItemsSource = magazine.foilData.Values.ToList<FoilData>();
        }

        private void OnViewChange(object sender, EventArgs e)
        {
            switch(ViewModePicker.SelectedIndex)
            {
                case 0:
                    HistoryCollection.ItemsSource = magazine.foilData.Values.ToList<FoilData>();
                    break;
                case 1:
                    HistoryCollection.ItemsSource = magazine.foilQueue;
                    break;
            }
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

                var t = (FoilData)ProductPicker.SelectedItem;
                if (t != null)
                {
                    magazine.AddFoil(new(t.Name, t.Index, t.Thickness, int.Parse(QuantityEntry.Text)));
                }

                await Task.Delay(2000);

                AddButton.Text = text;
                bIsSaving = false;
            }
        }

        private async void OnResetCliced(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Reset magazynu",
                "Czy na pewno wyzerowaæ wszystkie iloœci?",
                "Tak",
                "Anuluj");

            if (!confirm)
                return;

            magazine.ResetMagazine();

            await DisplayAlert("Gotowe", "Magazyn zosta³ wyzerowany.", "OK");
        }
    }
}
