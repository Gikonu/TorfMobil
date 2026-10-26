using System.Security.Cryptography;
using System.Text.Json;

namespace TorfMobile.Sync;

public static class FoilSyncMerge
{
    // „Sumuj” = suma ZBIORÓW wierszy/rolek, nie dodawanie Amount wg ProductCode.
    // Ten sam GlobalId trafia do wyniku tylko raz, nawet gdy operację powtórzymy.
    public static FoilSnapshot Sum(FoilSnapshot local, FoilSnapshot remote)
    {
        if (local.Epoch != remote.Epoch)
            throw new InvalidOperationException(
                "Urządzenia mają różne wersje zliczeń (po wcześniejszym nadpisaniu). " +
                "Wybierz kierunek nadpisania, aby je ponownie uzgodnić.");

        var byId = new Dictionary<string, FoilItem>(StringComparer.Ordinal);
        foreach (var item in local.Items.Concat(remote.Items))
        {
            if (string.IsNullOrWhiteSpace(item.GlobalId))
                throw new InvalidDataException("Rolka bez GlobalId.");

            if (byId.TryGetValue(item.GlobalId, out var current))
            {
                if (current.Index != item.Index ||
                    current.Name != item.Name ||
                    current.Amount != item.Amount)
                    throw new InvalidOperationException(
                        $"Konflikt zmian tej samej rolki ({item.GlobalId}). " +
                        "W tej wersji rozwiąż konflikt wybierając świadomie nadpisanie.");
            }
            else
            {
                byId.Add(item.GlobalId, new FoilItem
                {
                    GlobalId = item.GlobalId,
                    Index = item.Index,
                    Name = item.Name,
                    Amount = item.Amount
                });
            }
        }

        return new FoilSnapshot
        {
            Epoch = local.Epoch,
            Items = byId.Values.OrderBy(x => x.GlobalId, StringComparer.Ordinal).ToList()
        };
    }

    public static string Fingerprint(FoilSnapshot snapshot)
    {
        var normalized = new
        {
            snapshot.Epoch,
            Items = snapshot.Items.OrderBy(x => x.GlobalId, StringComparer.Ordinal)
                .Select(x => new { x.GlobalId, x.Index, x.Name, x.Amount }).ToArray()
        };
        return Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(normalized)));
    }
}
