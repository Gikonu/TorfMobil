using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;

namespace TorfMobile
{
    public class FoilMagazine
    {
        public class FoilData : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
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
        }

        public List<FoilData> foilData;
        public ObservableCollection<FoilData> foilQueue = new();

        private SqliteConnection dataBase;
        private SqliteCommand command;

        public FoilMagazine()
        {
            foilData = new List<FoilData>()
            {
            new("Ziemia uniwersalna 5L",                    "NCH.005.UNI", 50, 0),
            new("Ziemia uniwersalna 10L",                   "NCH.010.UNI", 50, 0),
            new("Ziemia uniwersalna 20L",                   "NCH.020.UNI", 60, 0),
            new("Ziemia uniwersalna 50L",                   "NCH.050.UNI", 80, 0),
            new("Ziemia uniwersalna 80L",                   "NCH.080.UNI", 80, 0),
            new("Torf kwaśny 20L",                          "NCH.020.TKW", 60, 0),
            new("Torf kwaśny 50L",                          "NCH.050.TKW", 80, 0),
            new("Torf kwaśny 80L",                          "NCH.080.TKW", 80, 0),
            new("Ziemia do roślin balkonowych 50L",         "NCH.050.BAL", 80, 0),
            new("Ziemia do iglaków 50L",                    "NCH.050.IGL", 80, 0),
            new("Torf odkwaszony 80L",                      "NCH.080.TOD", 80, 0),

            new("Ziemia uniwersalna 5L [NTM]",              "NTM.005.UNI", 50, 0),
            new("Ziemia uniwersalna 20L [NTM]",             "NTM.020.UNI", 60, 0),
            new("Ziemia uniwersalna 50L [NTM]",             "NTM.050.UNI", 80, 0),
            new("Torf kwaśny 20L [NTM]",                    "NTM.020.TKW", 60, 0),
            new("Torf kwaśny 50L [NTM]",                    "NTM.050.TKW", 80, 0),
            new("Ziemia do roślin balkonowych 20L [NTM]",   "NTM.020.BAL", 80, 0),
            new("Ziemia do roślin balkonowych 50L [NTM]",   "NTM.050.BAL", 80, 0),
            new("Ziemia do wysiewu 20L [NTM]",              "NTM.020.WYS", 60, 0),
            new("Ziemia do iglaków 50L [NTM]",              "NTM.050.IGL", 80, 0),

            new("Ziemia uniwersalna 20L [DIN]",             "DIN.020.UNI", 0, 0),
            new("Ziemia uniwersalna 50L [DIN]",             "DIN.050.UNI", 80, 0),

            new("Ziemia uniwersalna 20L [JER]",             "JER.020.UNI", 60, 0),

            new("Substrad do storczyków 5L [SUM]",          "SUM.005.STO", 80, 0),
            new("Ziemia do palm 5L [SUM]",                  "SUM.005.PAL", 80, 0),
            new("Ziemia uniwersalna 5L [SUM]",              "SUM.005.UNI", 80, 0),
            new("Ziemia uniwersalna 10L [SUM]",             "SUM.010.UNI", 80, 0),
            new("Ziemia uniwersalna 20L [SUM]",             "SUM.020.UNI", 80, 0),
            new("Ziemia uniwersalna 50L [SUM]",             "SUM.050.UNI", 90, 0),
            new("Ziemia uniwersalna 80L [SUM]",             "SUM.080.UNI", 90, 0),
            new("Ziemia do iglaków 20L [SUM]",              "SUM.020.IGL", 80, 0),
            new("Ziemia do iglaków 50L [SUM]",              "SUM.050.IGL", 90, 0),
            new("Ziemia do pelargoni 20L [SUM]",            "SUM.020.PSB", 80, 0),
            new("Ziemia do pelargoni 50L [SUM]",            "SUM.050.PSB", 90, 0),
            new("Ziemia do rododendronów 20L [SUM]",        "SUM.020.RAB", 80, 0),
            new("Ziemia do rododendronów 50L [SUM]",        "SUM.050.RAB", 90, 0),
            new("Torf kwaśny 20L [SUM]",                    "SUM.020.TKW", 80, 0),
            new("Torf kwaśny 50L [SUM]",                    "SUM.050.TKW", 90, 0),
            new("Ziemia do wysiewu i pikowania 20L [SUM]",  "SUM.020.WYS", 80, 0),
            new("Ziemia do warzyw 50L [SUM]",               "SUM.050.WAR", 90, 0),
            new("Ziemia do pomidorów 50L [SUM]",            "SUM.050.POM", 90, 0),
            new("Torf odkwaszony 80L [SUM]",                "SUM.005.TOD", 90, 0)
            };

            dataBase = new SqliteConnection("Data Source=Magazine.db");
            StartDataBase();
        }

        public void CloseDataBase()
        {
            SaveDataBase();
            dataBase.Close();
        }

        private void StartDataBase()
        {
            dataBase.Open();

            command = dataBase.CreateCommand();

            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Foil'";

            long exists = (long)command.ExecuteScalar();

            if (exists == 0)
            {
                command.CommandText =
                @"
                CREATE TABLE Foil
                (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Amount INTEGER NOT NULL
                )";

                command.ExecuteNonQuery();

                SaveDataBase();
            }
            else
            {
                LoadDataBase();
            }

        }

        private void SaveDataBase()
        {
            using var transaction = dataBase.BeginTransaction();
            command.Transaction = transaction;

            foreach (var v in foilData)
            {
                command.CommandText =
                @"INSERT INTO Foil(Id,Name,Amount)
                  VALUES($id,$name,$amount)
                  ON CONFLICT(Id)
                  DO UPDATE SET Amount = excluded.Amount;";

                command.Parameters.AddWithValue("$id", v.Index);
                command.Parameters.AddWithValue("$name", v.Name);
                command.Parameters.AddWithValue("$amount", v.Amount);

                command.ExecuteNonQuery();
                command.Parameters.Clear();
            }

            transaction.Commit();
        }

        private void LoadDataBase()
        {
            command.CommandText = "SELECT * FROM Foil";

            using var reader = command.ExecuteReader();

            while(reader.Read())
            {
                String id = (String)reader["Id"];
                long amount = (long)reader["Amount"];

                foreach(var v in foilData)
                {
                    if(v.Index == id)
                    {
                        v.Amount = (int)amount;
                        break;
                    }
                }
            }
        }
    }
}
