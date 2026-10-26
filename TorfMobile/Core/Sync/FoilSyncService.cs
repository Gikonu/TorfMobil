using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using TorfMobile.Network;

namespace TorfMobile.Sync;

/// <summary>
/// Synchronizuje POSZCZEGÓLNE wiersze (rolki) tabeli Foil pomiędzy połączonymi urządzeniami.
/// Działa w obie strony w ramach istniejącego TCP; brak nowych portów.
/// </summary>
public sealed class FoilSyncService
{
    private const string TransportType = "sync.foil.v2";
    private readonly NetworkService _network;
    private readonly FoilSqliteSyncStore _store;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<SyncEnvelope>> _pending = new();
    private readonly ConcurrentDictionary<string, string> _approved = new();
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public event Action<string>? Diagnostic;
    // Podepnij tutaj odczyt odświeżonych danych do FoilMagazine po zapisie w SQLite.
    public event Action? FoilDataChanged;
    private void RaiseFoilDataChanged() => MainThread.BeginInvokeOnMainThread(() => FoilDataChanged?.Invoke());

    public FoilSyncService(NetworkService network, FoilSqliteSyncStore store)
    {
        _network = network;
        _store = store;
        _network.Connections.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(LanDevice sender, TcpMessage message)
    {
        if (message.Type != TransportType) return;
        SyncEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<SyncEnvelope>(message.Text); }
        catch (Exception ex) { Diagnostic?.Invoke($"Błędna wiadomość: {ex.Message}"); return; }
        if (envelope is null) return;

        if (!string.IsNullOrWhiteSpace(envelope.ReplyTo))
        {
            if (_pending.TryGetValue(envelope.ReplyTo, out var waiter)) waiter.TrySetResult(envelope);
            return;
        }
        _ = HandleRequestSafelyAsync(sender, envelope);
    }

    private async Task HandleRequestSafelyAsync(LanDevice sender, SyncEnvelope request)
    {
        SyncEnvelope response = new()
        {
            ReplyTo = request.MessageId,
            OperationId = request.OperationId,
            Category = request.Category,
            Mode = request.Mode,
            Kind = "error",
            Success = false
        };
        try
        {
            if (request.Category != "foil" || (request.Mode != "sum" && request.Mode != "overwrite"))
                throw new InvalidOperationException("Nieobsługiwana kategoria lub typ synchronizacji.");
            if (string.IsNullOrWhiteSpace(request.OperationId))
                throw new InvalidOperationException("Brak identyfikatora operacji.");

            switch (request.Kind)
            {
                case "snapshot.request":
                    if (!await AskPermissionAsync(sender, request))
                        throw new InvalidOperationException("Użytkownik drugiego urządzenia odrzucił synchronizację.");
                    _approved[request.OperationId] = sender.Id;
                    response.Kind = "snapshot.response";
                    response.Snapshot = await Task.Run(() => _store.Read(_network.LocalDevice.Id));
                    response.Success = true;
                    break;

                case "apply.request":
                    if (!_approved.TryRemove(request.OperationId, out var authorizedId) || authorizedId != sender.Id)
                        throw new InvalidOperationException("Brak zgody na tę operację.");
                    if (request.Snapshot is null)
                        throw new InvalidDataException("Brak danych do synchronizacji.");
                    if (string.IsNullOrWhiteSpace(request.ExpectedHash))
                        throw new InvalidDataException("Brak sumy kontrolnej poprzedniego stanu.");
                    await Task.Run(() => _store.Apply(_network.LocalDevice.Id, request.ExpectedHash, request.Snapshot));
                    RaiseFoilDataChanged();
                    response.Kind = "apply.response";
                    response.Success = true;
                    break;

                default:
                    throw new InvalidDataException("Nieznany typ komunikatu synchronizacji.");
            }
        }
        catch (Exception ex) { response.Error = ex.Message; Diagnostic?.Invoke(ex.Message); }
        try { await SendAsync(sender.Id, response); }
        catch (Exception ex) { Diagnostic?.Invoke($"Nie mogę wysłać odpowiedzi: {ex.Message}"); }
    }

    private static async Task<bool> AskPermissionAsync(LanDevice sender, SyncEnvelope request)
    {
        string description = request.Mode == "sum"
            ? $"{sender.Name} chce zsumować zliczenia FOLIA z Twoimi danymi. Zmiany zostaną zapisane na obu urządzeniach."
            : $"{sender.Name} chce przenieść zliczenia FOLIA. Źródło: {request.SourceName}. Cel: {request.TargetName}. Dane na urządzeniu docelowym zostaną nadpisane.";

        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            Page? page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null) return false;
            return await page.DisplayAlert("Zgoda na synchronizację", description, "Zezwól", "Odrzuć");
        });
    }

    /// <param name="sourceIsLocal">W trybie Nadpisz: true = moje urządzenie -> drugie, false = drugie -> moje.</param>
    public async Task SynchronizeAsync(LanDevice peer, string mode, bool sourceIsLocal,
        CancellationToken cancellationToken = default)
    {
        if (mode != "sum" && mode != "overwrite")
            throw new ArgumentException("Nieznany tryb synchronizacji.", nameof(mode));
        if (!_network.Connections.IsConnected(peer.Id))
            throw new InvalidOperationException("Najpierw połącz się z tym urządzeniem przez TCP.");
        if (!await _syncGate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Inna synchronizacja już trwa na tym urządzeniu.");

        try
        {
            string operationId = Guid.NewGuid().ToString("N");
            var local = await Task.Run(() => _store.Read(_network.LocalDevice.Id), cancellationToken);
            var remoteResponse = await RequestAsync(peer.Id, new SyncEnvelope
            {
                Kind = "snapshot.request", OperationId = operationId,
                Mode = mode, Category = "foil",
                SourceDeviceId = mode == "sum" ? "" : (sourceIsLocal ? _network.LocalDevice.Id : peer.Id),
                SourceName = mode == "sum" ? "Oba urządzenia" : (sourceIsLocal ? _network.LocalDevice.Name : peer.Name),
                TargetName = mode == "sum" ? "Oba urządzenia" : (sourceIsLocal ? peer.Name : _network.LocalDevice.Name)
            }, cancellationToken);
            if (remoteResponse.Kind != "snapshot.response" || remoteResponse.Snapshot is null)
                throw new InvalidDataException("Drugie urządzenie nie zwróciło zliczeń.");

            FoilSnapshot remote = remoteResponse.Snapshot;
            FoilSnapshot result = mode == "sum"
                ? FoilSyncMerge.Sum(local, remote)
                : JsonSerializer.Deserialize<FoilSnapshot>(
                    JsonSerializer.Serialize(sourceIsLocal ? local : remote))!;
            // Nadpisanie tworzy nową wersję kategorii. Stare urządzenia z poprzednią
            // wersją nie mogą potem przypadkiem przywrócić usuniętych wkładów przez sumowanie.
            if (mode == "overwrite") result.Epoch = Guid.NewGuid().ToString("N");
            string localHash = FoilSyncMerge.Fingerprint(local);
            string remoteHash = FoilSyncMerge.Fingerprint(remote);

            // Najpierw zapis zdalny, potem lokalny. W trybie overwrite urządzenie
            // źródłowe zachowuje swoje ilości, lecz przyjmuje nową wersję (Epoch).
            // Jeśli połączenie padnie po zdalnym zapisie, ponowne „Sumuj”
            // nie dubluje tych samych rolek: identyfikuje je po GlobalId.
            var applyResult = await RequestAsync(peer.Id, new SyncEnvelope
            {
                Kind = "apply.request", OperationId = operationId, Mode = mode,
                Category = "foil", Snapshot = result, ExpectedHash = remoteHash
            }, cancellationToken);
            if (applyResult.Kind != "apply.response")
                throw new InvalidDataException("Brak potwierdzenia zapisu na drugim urządzeniu.");

            await Task.Run(() => _store.Apply(_network.LocalDevice.Id, localHash, result), cancellationToken);
            RaiseFoilDataChanged();
        }
        finally { _syncGate.Release(); }
    }

    private async Task<SyncEnvelope> RequestAsync(string peerId, SyncEnvelope request,
        CancellationToken cancellationToken)
    {
        var waiter = new TaskCompletionSource<SyncEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(request.MessageId, waiter)) throw new InvalidOperationException("Powielone żądanie.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2)); // Czekanie na potwierdzenie na drugim ekranie
            await SendAsync(peerId, request, timeout.Token);
            var response = await waiter.Task.WaitAsync(timeout.Token);
            if (!response.Success) throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Error) ? "Synchronizacja została odrzucona." : response.Error);
            return response;
        }
        finally { _pending.TryRemove(request.MessageId, out _); }
    }

    private Task SendAsync(string deviceId, SyncEnvelope envelope, CancellationToken cancellationToken = default)
        => _network.Connections.SendMessageAsync(
            deviceId, TransportType, JsonSerializer.Serialize(envelope), cancellationToken);
}
