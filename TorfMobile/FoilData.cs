using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorfMobile
{
    public class FoilData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public long Id = 0;
        public string Name { get; }
        public string Index { get; }
        public int Thickness { get; }
        private int amount;
        public int Amount
        {
            get => amount;
            set
            {
                amount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Amount)));
            }
        }

        public FoilData()
        {
            Name = "Error";
            Index = "Error";
            Thickness = 0;
            Amount = 0;
        }

        public FoilData(string name, string index, int thickness, int amount)
        {
            Name = name;
            Index = index;
            Thickness = thickness;
            Amount = amount;
        }

        public FoilData(string name, string index, int thickness, int amount, long id)
        {
            Name = name;
            Index = index;
            Thickness = thickness;
            Amount = amount;
            Id = id;
        }
    }
}
