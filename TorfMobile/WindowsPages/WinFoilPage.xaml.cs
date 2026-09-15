
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace TorfMobile
{
	public partial class WinFoilPage : ContentPage
	{
        FoilMagazine magazine;
        bool bIsSaving = false;

        public WinFoilPage(FoilMagazine magazine)
		{
			InitializeComponent();

            this.magazine = magazine;

            ProductPicker.ItemsSource = magazine.foilData.Values.ToList<FoilData>();
            ProductPicker.ItemDisplayBinding = new Binding("Name");

            HistoryCollection.ItemsSource = magazine.foilData.Values.ToList<FoilData>();
        }
        private void OnProductChange(object sender, EventArgs e)
        {
            FoilData t = (FoilData)(ProductPicker.SelectedItem);
            EntryX.Text = t.Thickness.ToString();
            EntryY.Text = "105";
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

        private void OnResultCliced(object sender, EventArgs e)
        {
            if (EntryX.Text != null && EntryY.Text != null && EntryZ.Text != null)
            {
                double x, y, z, o;
                x = double.Parse(EntryX.Text) / 1000000;
                y = double.Parse(EntryY.Text) / 1000;
                z = double.Parse(EntryZ.Text) / 1000;

                o = Math.PI * Math.Pow(z / 2, 2);
                o -= Math.PI * Math.Pow(y / 2, 2);

                if (x == 0)
                    return;

                o = o / x;

                int result = (int)o;
                QuantityEntry.Text = result.ToString();
            }
        }
    }
}
