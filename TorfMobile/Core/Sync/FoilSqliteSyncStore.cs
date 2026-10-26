using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;

namespace TorfMobile.Sync;

/// <summary>
/// Obsługa rzeczywistego schematu TorfMobile:
/// Foil(Id INTEGER PRIMARY KEY, ProductCode TEXT, Name TEXT, Amount INTEGER).
/// JEDEN wiersz = JEDNA rolka. Wiersze mogą mieć ten sam ProductCode.
/// Dodajemy wyłącznie opcjonalną kolumnę SyncGlobalId (bez utraty danych).
/// Nie zmieniamy katalogu produktów FoilMagazine ani innych kategorii.
/// </summary>
public sealed class FoilSqliteSyncStore
{
    private const int MaxRows = 10000;
    private readonly object _gate = new();
    private readonly string _databasePath;

    public FoilSqliteSyncStore()
        : this(Path.Combine(FileSystem.AppDataDirectory, "Magazine.db")) { }

    public FoilSqliteSyncStore(string databasePath) => _databasePath = databasePath;

    // localDeviceId pozostaje w sygnaturze ze względu na zgodność z FoilSyncService.
    public FoilSnapshot Read(string localDeviceId)
    {
        lock (_gate)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            EnsureSchema(db, tx);
            var snapshot = ReadSnapshot(db, tx);
            tx.Commit();
            return snapshot;
        }
    }

    public void Apply(string localDeviceId, string expectedHash, FoilSnapshot replacement)
    {
        ValidateSnapshot(replacement);

        lock (_gate)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            EnsureSchema(db, tx);

            var before = ReadSnapshot(db, tx);
            if (FoilSyncMerge.Fingerprint(before) != expectedHash)
                throw new InvalidOperationException(
                    "Zliczenia na tym urządzeniu zmieniły się podczas synchronizacji. " +
                    "Ponów synchronizację.");

            var beforeById = before.Items.ToDictionary(x => x.GlobalId, StringComparer.Ordinal);
            var replacementById = replacement.Items.ToDictionary(x => x.GlobalId, StringComparer.Ordinal);

            // Usuń tylko te ROLKI, których nie ma w uzgodnionym wyniku.
            // W trybie „Nadpisz” to celowe zastąpienie całej kategorii Folia.
            foreach (var old in before.Items)
            {
                if (replacementById.ContainsKey(old.GlobalId)) continue;
                using var delete = Cmd(db, tx,
                    "DELETE FROM Foil WHERE SyncGlobalId = $guid");
                delete.Parameters.AddWithValue("$guid", old.GlobalId);
                delete.ExecuteNonQuery();
            }

            // Te same rolki zachowują swoje lokalne Id; nowe dostają własne Id SQLite.
            foreach (var item in replacement.Items)
            {
                if (beforeById.TryGetValue(item.GlobalId, out var existing))
                {
                    if (existing.Index == item.Index && existing.Name == item.Name &&
                        existing.Amount == item.Amount)
                        continue;

                    using var update = Cmd(db, tx, @"
                        UPDATE Foil
                        SET ProductCode = $code, Name = $name, Amount = $amount
                        WHERE SyncGlobalId = $guid");
                    SetRowParameters(update, item);
                    if (update.ExecuteNonQuery() != 1)
                        throw new InvalidDataException("Nie udało się zaktualizować rolki.");
                }
                else
                {
                    // Id NIE kopiujemy z obcego urządzenia: mogą występować kolizje.
                    using var insert = Cmd(db, tx, @"
                        INSERT INTO Foil (ProductCode, Name, Amount, SyncGlobalId)
                        VALUES ($code, $name, $amount, $guid)");
                    SetRowParameters(insert, item);
                    insert.ExecuteNonQuery();
                }
            }

            using (var setEpoch = Cmd(db, tx, @"
                INSERT INTO SyncFoilMeta(Key, Value) VALUES ('Epoch', $epoch)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value"))
            {
                setEpoch.Parameters.AddWithValue("$epoch", replacement.Epoch);
                setEpoch.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    private static void SetRowParameters(SqliteCommand cmd, FoilItem item)
    {
        cmd.Parameters.AddWithValue("$guid", item.GlobalId);
        cmd.Parameters.AddWithValue("$code", item.Index);
        cmd.Parameters.AddWithValue("$name", item.Name);
        cmd.Parameters.AddWithValue("$amount", item.Amount);
    }

    private SqliteConnection Open()
    {
        if (!File.Exists(_databasePath))
            throw new FileNotFoundException(
                "Brak Magazine.db. Uruchom najpierw inicjalizację bazy DataBase.",
                _databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            DefaultTimeout = 10
        };
        var db = new SqliteConnection(connectionString.ToString());
        db.Open();
        return db;
    }

    private static SqliteCommand Cmd(SqliteConnection db, SqliteTransaction tx, string sql)
        => new(sql, db, tx);

    private static void Execute(SqliteConnection db, SqliteTransaction tx, string sql)
    {
        using var cmd = Cmd(db, tx, sql);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureSchema(SqliteConnection db, SqliteTransaction tx)
    {
        // Kontrolujemy prawdziwy schemat z przesłanej klasy DataBase.cs.
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragma = Cmd(db, tx, "PRAGMA table_info(Foil)"))
        using (var reader = pragma.ExecuteReader())
            while (reader.Read()) fields.Add(reader.GetString(1));

        foreach (var required in new[] { "Id", "ProductCode", "Name", "Amount" })
        {
            if (!fields.Contains(required))
                throw new InvalidDataException(
                    $"Tabela Foil nie zawiera kolumny {required}. " +
                    "Sprawdź wersję DataBase.cs.");
        }

        // Migracja bez kasowania tabeli/wierszy; stare AddFoilTableRow działa bez zmian.
        if (!fields.Contains("SyncGlobalId"))
            Execute(db, tx, "ALTER TABLE Foil ADD COLUMN SyncGlobalId TEXT NULL");

        Execute(db, tx, @"
            CREATE TABLE IF NOT EXISTS SyncFoilMeta (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL
            )");

        // Każda nowa rolka z FoilMagazine.AddFoil ma początkowo NULL.
        // Dostaje GUID przy pierwszym odczycie/synchronizacji.
        var pendingIds = new List<long>();
        using (var select = Cmd(db, tx, @"
            SELECT Id FROM Foil
            WHERE SyncGlobalId IS NULL OR trim(SyncGlobalId) = ''"))
        using (var reader = select.ExecuteReader())
            while (reader.Read()) pendingIds.Add(reader.GetInt64(0));

        foreach (long id in pendingIds)
        {
            using var update = Cmd(db, tx,
                "UPDATE Foil SET SyncGlobalId = $guid WHERE Id = $id");
            update.Parameters.AddWithValue("$guid", Guid.NewGuid().ToString("N"));
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }

        Execute(db, tx, @"
            CREATE UNIQUE INDEX IF NOT EXISTS UX_Foil_SyncGlobalId
            ON Foil(SyncGlobalId)");
    }

    private static FoilSnapshot ReadSnapshot(SqliteConnection db, SqliteTransaction tx)
    {
        string epoch;
        using (var meta = Cmd(db, tx,
            "SELECT Value FROM SyncFoilMeta WHERE Key = 'Epoch'"))
            epoch = meta.ExecuteScalar() as string ?? "initial";

        var result = new FoilSnapshot { Epoch = epoch };
        using (var cmd = Cmd(db, tx, @"
            SELECT SyncGlobalId, ProductCode, Name, Amount
            FROM Foil ORDER BY Id"))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Items.Add(new FoilItem
                {
                    GlobalId = reader.GetString(0),
                    Index = reader.GetString(1),
                    Name = reader.GetString(2),
                    Amount = reader.GetInt64(3)
                });
                if (result.Items.Count > MaxRows)
                    throw new InvalidDataException(
                        $"Za dużo rolek do synchronizacji (limit {MaxRows}).");
            }
        }
        ValidateSnapshot(result);
        return result;
    }

    private static void ValidateSnapshot(FoilSnapshot snapshot)
    {
        if (snapshot is null || snapshot.Items is null ||
            string.IsNullOrWhiteSpace(snapshot.Epoch) || snapshot.Epoch.Length > 100 ||
            snapshot.Items.Count > MaxRows)
            throw new InvalidDataException("Niepoprawna migawka zliczeń.");

        var guids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (item is null ||
                !Guid.TryParseExact(item.GlobalId, "N", out _) ||
                !guids.Add(item.GlobalId) ||
                string.IsNullOrWhiteSpace(item.Index) || item.Index.Length > 200 ||
                string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 300 ||
                item.Amount is < 0 or > int.MaxValue)
                throw new InvalidDataException(
                    "Zliczenia zawierają nieprawidłową, zduplikowaną lub zbyt dużą rolkę.");
        }
    }
}
