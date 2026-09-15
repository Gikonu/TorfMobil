
using Microsoft.Maui.Layouts;

namespace TorfMobile
{
    public partial class AndFoilCalculatorPage : ContentPage
    {
        FoilMagazine magazine;
        int result = 0;

        bool bIsSaving = false;

        public AndFoilCalculatorPage(FoilMagazine magazine)
        {
            InitializeComponent();
            this.magazine = magazine;

            ProductPicker.ItemsSource = magazine.foilData.Values.ToList<FoilData>();
            ProductPicker.ItemDisplayBinding = new Binding("Name");
        }
        private async void OnSaveCliced(object sender, EventArgs e)
        {
            if (bIsSaving || result == 0)
                return;

            string text = SaveButton.Text;
            SaveButton.Text = "Zapisane";
            bIsSaving = true;

            var t = (FoilData)ProductPicker.SelectedItem;
            if (t != null)
            {
                magazine.AddFoil(new(t.Name, t.Index, t.Thickness, result));
            }

            await Task.Delay(2000);

            SaveButton.Text = text;
            bIsSaving = false;
        }

        private void OnProductSelected(object sender, EventArgs e)
        {
            if (ProductPicker.SelectedIndex != -1)
            {
                FoilData v = (FoilData)ProductPicker.SelectedItem;
                if (v != null)
                {
                    EntryX.Text = v.Thickness.ToString();
                    EntryY.Text = "105";
                    result = 0;
                    OutLabel.Text = result.ToString() + " mb";
                }
            }
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

                result = (int)o;
                OutLabel.Text = result.ToString() + " mb";
            }
        }
    }
}
