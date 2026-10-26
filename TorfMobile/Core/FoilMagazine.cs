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
        public Dictionary<String,FoilData> foilData;
        public ObservableCollection<FoilData> foilQueue = new();

        private DataBase dataBase;

        private long nextId = 0;

        public FoilMagazine(DataBase dataBase)
        {
            foilData = new Dictionary<string, FoilData>()
            {
            ["NCH.005.UNI"] = new("Ziemia uniwersalna 5L",                    "NCH.005.UNI", 50, 0),
            ["NCH.010.UNI"] = new("Ziemia uniwersalna 10L",                   "NCH.010.UNI", 50, 0),
            ["NCH.020.UNI"] = new("Ziemia uniwersalna 20L",                   "NCH.020.UNI", 60, 0),
            ["NCH.050.UNI"] = new("Ziemia uniwersalna 50L",                   "NCH.050.UNI", 80, 0),
            ["NCH.080.UNI"] = new("Ziemia uniwersalna 80L",                   "NCH.080.UNI", 80, 0),
            ["NCH.020.TKW"] = new("Torf kwaśny 20L",                          "NCH.020.TKW", 60, 0),
            ["NCH.050.TKW"] = new("Torf kwaśny 50L",                          "NCH.050.TKW", 80, 0),
            ["NCH.080.TKW"] = new("Torf kwaśny 80L",                          "NCH.080.TKW", 80, 0),
            ["NCH.050.BAL"] = new("Ziemia do roślin balkonowych 50L",         "NCH.050.BAL", 80, 0),
            ["NCH.050.IGL"] = new("Ziemia do iglaków 50L",                    "NCH.050.IGL", 80, 0),
            ["NCH.080.TOD"] = new("Torf odkwaszony 80L",                      "NCH.080.TOD", 80, 0),

            ["NTM.005.UNI"] = new("Ziemia uniwersalna 5L [NTM]",              "NTM.005.UNI", 50, 0),
            ["NTM.020.UNI"] = new("Ziemia uniwersalna 20L [NTM]",             "NTM.020.UNI", 60, 0),
            ["NTM.050.UNI"] = new("Ziemia uniwersalna 50L [NTM]",             "NTM.050.UNI", 80, 0),
            ["NTM.020.TKW"] = new("Torf kwaśny 20L [NTM]",                    "NTM.020.TKW", 60, 0),
            ["NTM.050.TKW"] = new("Torf kwaśny 50L [NTM]",                    "NTM.050.TKW", 80, 0),
            ["NTM.020.BAL"] = new("Ziemia do roślin balkonowych 20L [NTM]",   "NTM.020.BAL", 80, 0),
            ["NTM.050.BAL"] = new("Ziemia do roślin balkonowych 50L [NTM]",   "NTM.050.BAL", 80, 0),
            ["NTM.020.WYS"] = new("Ziemia do wysiewu 20L [NTM]",              "NTM.020.WYS", 60, 0),
            ["NTM.050.IGL"] = new("Ziemia do iglaków 50L [NTM]",              "NTM.050.IGL", 80, 0),

            ["DIN.020.UNI"] = new("Ziemia uniwersalna 20L [DIN]",             "DIN.020.UNI", 0, 0),
            ["DIN.050.UNI"] = new("Ziemia uniwersalna 50L [DIN]",             "DIN.050.UNI", 80, 0),

            ["JER.020.UNI"] = new("Ziemia uniwersalna 20L [JER]",             "JER.020.UNI", 60, 0),

            ["SUM.005.STO"] = new("Substrad do storczyków 5L [SUM]",          "SUM.005.STO", 80, 0),
            ["SUM.005.PAL"] = new("Ziemia do palm 5L [SUM]",                  "SUM.005.PAL", 80, 0),
            ["SUM.005.UNI"] = new("Ziemia uniwersalna 5L [SUM]",              "SUM.005.UNI", 80, 0),
            ["SUM.010.UNI"] = new("Ziemia uniwersalna 10L [SUM]",             "SUM.010.UNI", 80, 0),
            ["SUM.020.UNI"] = new("Ziemia uniwersalna 20L [SUM]",             "SUM.020.UNI", 80, 0),
            ["SUM.050.UNI"] = new("Ziemia uniwersalna 50L [SUM]",             "SUM.050.UNI", 90, 0),
            ["SUM.080.UNI"] = new("Ziemia uniwersalna 80L [SUM]",             "SUM.080.UNI", 90, 0),
            ["SUM.020.IGL"] = new("Ziemia do iglaków 20L [SUM]",              "SUM.020.IGL", 80, 0),
            ["SUM.050.IGL"] = new("Ziemia do iglaków 50L [SUM]",              "SUM.050.IGL", 90, 0),
            ["SUM.020.PSB"] = new("Ziemia do pelargoni 20L [SUM]",            "SUM.020.PSB", 80, 0),
            ["SUM.050.PSB"] = new("Ziemia do pelargoni 50L [SUM]",            "SUM.050.PSB", 90, 0),
            ["SUM.020.RAB"] = new("Ziemia do rododendronów 20L [SUM]",        "SUM.020.RAB", 80, 0),
            ["SUM.050.RAB"] = new("Ziemia do rododendronów 50L [SUM]",        "SUM.050.RAB", 90, 0),
            ["SUM.020.TKW"] = new("Torf kwaśny 20L [SUM]",                    "SUM.020.TKW", 80, 0),
            ["SUM.050.TKW"] = new("Torf kwaśny 50L [SUM]",                    "SUM.050.TKW", 90, 0),
            ["SUM.020.WYS"] = new("Ziemia do wysiewu i pikowania 20L [SUM]",  "SUM.020.WYS", 80, 0),
            ["SUM.050.WAR"] = new("Ziemia do warzyw 50L [SUM]",               "SUM.050.WAR", 90, 0),
            ["SUM.050.POM"] = new("Ziemia do pomidorów 50L [SUM]",            "SUM.050.POM", 90, 0),
            ["SUM.005.TOD"] = new("Torf odkwaszony 80L [SUM]",                "SUM.005.TOD", 90, 0)
            };

            this.dataBase = dataBase;
            LoadDataBase();
        }

        public void ResetMagazine()
        {
            foilQueue.Clear();
            foreach (var foil in foilData)
            {
                foil.Value.Amount = 0;              
            }
            nextId = 0;
            dataBase.ClearFoilTable();
        }

        public void AddFoil(FoilData foil)
        {
            foil.Id = nextId;
            nextId++;
            foilQueue.Insert(0,foil);
            foilData[foil.Index].Amount += foil.Amount;

            dataBase.AddFoilTableRow(foil);
        }

        public void ReloadFromDatabase()
        {
            foilQueue.Clear();
            foreach (var foil in foilData.Values)
                foil.Amount = 0;
            LoadDataBase();
        }

        private void LoadDataBase()
        {
            nextId = dataBase.GetDataFromFoilTable(foilQueue);
            foreach (var foil in foilQueue)
            {
                foilData[foil.Index].Amount += foil.Amount;
            }
        }
    }
}
