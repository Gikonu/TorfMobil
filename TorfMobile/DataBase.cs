using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace TorfMobile
{
    public class DataBase : IDisposable
    {
        private SqliteConnection dataBase;
        private const int CurrentVersion = 1; //31.08.2026

        public DataBase() 
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Magazine.db");
            dataBase = new SqliteConnection($"Data Source={dbPath}");
            dataBase.Open();

            CheckVersion();
        }

        public void Dispose()
        {
            dataBase.Dispose();
        }

        private void CheckVersion()
        {
            using var transaction = dataBase.BeginTransaction();
            try 
            {
                SqliteCommand command = dataBase.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Version'";

                if ((long?)command.ExecuteScalar() == 0)
                {
                    HardUpdate(transaction);

                    command.CommandText =
                    @"
                    CREATE TABLE Version
                    (
                    Id INTEGER PRIMARY KEY,
                    Value INTEGER NOT NULL
                    )";

                    command.ExecuteNonQuery();
                    command.CommandText =
                    @"INSERT INTO Version(Id,Value)
                        VALUES($id,$value)";

                    command.Parameters.AddWithValue("$id", 1);
                    command.Parameters.AddWithValue("$value", CurrentVersion);

                    command.ExecuteNonQuery();
                    command.Parameters.Clear();
                }
                else
                {
                    command.CommandText = "SELECT Value FROM Version WHERE Id = 1";

                    if ((long?)command.ExecuteScalar() != CurrentVersion)
                    {
                        HardUpdate(transaction); // Tymczasow

                        command.CommandText = "UPDATE Version SET Value = $value WHERE Id = 1";
                        command.Parameters.AddWithValue("$value", CurrentVersion);

                        command.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private void HardUpdate(SqliteTransaction transaction)
        {
            SqliteCommand command = dataBase.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = "DROP TABLE IF EXISTS Foil";
            command.ExecuteNonQuery();

            command.CommandText =
            @"
            CREATE TABLE Foil
            (
                Id INTEGER PRIMARY KEY,
                ProductCode TEXT NOT NULL,
                Name TEXT NOT NULL,
                Amount INTEGER NOT NULL
            )
            ";

            command.ExecuteNonQuery();
        }

        public void ClearFoilTable()
        {
            SqliteCommand command = dataBase.CreateCommand();

            command.CommandText = "DELETE FROM Foil";

            command.ExecuteNonQuery();
        }

        public void AddFoilTableRow(FoilData data)
        {
            SqliteCommand command = dataBase.CreateCommand();
            command.CommandText =
                @"INSERT INTO Foil(Id,ProductCode,Name,Amount)
                  VALUES($id,$code,$name,$amount)";

            command.Parameters.AddWithValue("$id", data.Id);
            command.Parameters.AddWithValue("$code", data.Index);
            command.Parameters.AddWithValue("$name", data.Name);
            command.Parameters.AddWithValue("$amount", data.Amount);

            command.ExecuteNonQuery();
        }

        public long GetDataFromFoilTable(ObservableCollection<FoilData> foilQueue)
        {
            long nextId = 0;

            SqliteCommand command = dataBase.CreateCommand();
            command.CommandText = "SELECT * FROM Foil ORDER BY Id";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                long id = (long)reader["Id"];
                String index = (String)reader["ProductCode"];
                String name = (String)reader["Name"];
                int amount = (int)(long)reader["Amount"];

                foilQueue.Insert(0,new(name, index, 0, amount, id));

                nextId = id;
            }

            return ++nextId;
        }
    }
}
