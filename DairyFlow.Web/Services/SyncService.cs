using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;

namespace DairyFlow.Web.Services;

// ─────────────────────────────────────────────────────────────────────────────
// IOfflineStorageService — Wraps IndexedDB via JS Interop
// ─────────────────────────────────────────────────────────────────────────────
public interface IOfflineStorageService
{
    Task<List<T>> GetAllAsync<T>(string storeName);
    Task SaveAsync<T>(string storeName, T item);
    Task DeleteAsync(string storeName, Guid id);
    Task AddToSyncQueueAsync(SyncOperation operation);
    Task<List<SyncOperation>> GetPendingSyncItemsAsync();
    Task MarkSyncItemCompleteAsync(string localId, Guid serverId);
    Task<int> GetSyncQueueCountAsync();
    Task<string?> GetMetaAsync(string key);
    Task SetMetaAsync(string key, string value);
    Task LoadSnapshotAsync(DataSnapshot snapshot);
    Task ClearAllDataAsync();
}

public class OfflineStorageService : IOfflineStorageService
{
    private readonly IJSRuntime _js;

    public OfflineStorageService(IJSRuntime js) => _js = js;

    public async Task<List<T>> GetAllAsync<T>(string storeName)
    {
        var result = await _js.InvokeAsync<JsonElement>($"DairyFlowDB.getAll{storeName}");
        return JsonSerializer.Deserialize<List<T>>(result.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task SaveAsync<T>(string storeName, T item)
        => await _js.InvokeVoidAsync($"DairyFlowDB.save{storeName[..^1]}", item); // plurals → singular

    public async Task DeleteAsync(string storeName, Guid id)
        => await _js.InvokeVoidAsync($"DairyFlowDB.delete{storeName[..^1]}", id.ToString());

    public async Task AddToSyncQueueAsync(SyncOperation operation)
        => await _js.InvokeVoidAsync("DairyFlowDB.addToSyncQueue", operation);

    public async Task<List<SyncOperation>> GetPendingSyncItemsAsync()
    {
        var result = await _js.InvokeAsync<JsonElement>("DairyFlowDB.getPendingSyncItems");
        return JsonSerializer.Deserialize<List<SyncOperation>>(result.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task MarkSyncItemCompleteAsync(string localId, Guid serverId)
        => await _js.InvokeVoidAsync("DairyFlowDB.markSyncItemComplete", localId, serverId.ToString());

    public async Task<int> GetSyncQueueCountAsync()
        => await _js.InvokeAsync<int>("DairyFlowDB.getSyncQueueCount");

    public async Task<string?> GetMetaAsync(string key)
        => await _js.InvokeAsync<string?>("DairyFlowDB.getMeta", key);

    public async Task SetMetaAsync(string key, string value)
        => await _js.InvokeVoidAsync("DairyFlowDB.setMeta", key, value);

    public async Task LoadSnapshotAsync(DataSnapshot snapshot)
        => await _js.InvokeVoidAsync("DairyFlowDB.loadSnapshot", snapshot);

    public async Task ClearAllDataAsync()
        => await _js.InvokeVoidAsync("DairyFlowDB.clearAllData");
}

// ─────────────────────────────────────────────────────────────────────────────
// ISyncService — Handles sync between IndexedDB and server
// ─────────────────────────────────────────────────────────────────────────────
public interface ISyncService
{
    event Action<SyncStatus>? SyncStatusChanged;
    SyncStatus CurrentStatus { get; }
    int PendingCount { get; }
    Task InitializeAsync();
    Task SyncNowAsync();
    Task<bool> IsOnlineAsync();

    // Queue an offline change (or a change whose online save just failed) and, if we're online,
    // push it straight away. This is how pages record a create/update/delete that must survive
    // a dropped connection.
    Task QueueChangeAsync(string entityType, string operation, Guid entityId, object payload);
}

public enum SyncStatus { Synced, Syncing, Pending, Offline, Error }

public class SyncService : ISyncService, IDisposable
{
    private readonly IOfflineStorageService _storage;
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly ILogger<SyncService> _logger;
    private readonly Timer _syncTimer;
    private bool _isOnline = true;
    private DotNetObjectReference<SyncService>? _selfRef;

    public event Action<SyncStatus>? SyncStatusChanged;
    public SyncStatus CurrentStatus { get; private set; } = SyncStatus.Synced;
    public int PendingCount { get; private set; } = 0;

    public SyncService(
        IOfflineStorageService storage,
        HttpClient http,
        IJSRuntime js,
        ILogger<SyncService> logger)
    {
        _storage = storage;
        _http = http;
        _js = js;
        _logger = logger;

        // Auto-sync every 30 seconds when online
        _syncTimer = new Timer(_ => _ = TrySyncAsync(), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task InitializeAsync()
    {
        // Register JS online/offline listeners against THIS instance. (The old code invoked a
        // static method that doesn't exist, so reconnect-sync never fired — passing a
        // DotNetObjectReference lets the browser call our instance methods directly.)
        _selfRef = DotNetObjectReference.Create(this);
        try { await _js.InvokeVoidAsync("DairyFlowSync.registerConnectivity", _selfRef); }
        catch { /* JS bridge missing — sync still works via the 30s timer */ }

        // First run on this device: pull a full snapshot so the app has data to show offline.
        var lastSync = await _storage.GetMetaAsync("lastSyncAt");
        if (lastSync == null)
        {
            await LoadFullSnapshotAsync();
        }

        _isOnline = await IsOnlineAsync();
        PendingCount = await _storage.GetSyncQueueCountAsync();
        UpdateStatus();
    }

    public async Task QueueChangeAsync(string entityType, string operation, Guid entityId, object payload)
    {
        await _storage.AddToSyncQueueAsync(new SyncOperation
        {
            Operation = operation,
            EntityType = entityType,
            ServerId = entityId,
            Payload = payload
        });

        PendingCount = await _storage.GetSyncQueueCountAsync();

        if (await IsOnlineAsync())
            await SyncNowAsync();
        else
            UpdateStatus();
    }

    public async Task SyncNowAsync()
    {
        if (CurrentStatus == SyncStatus.Syncing) return;
        if (!await IsOnlineAsync()) return;

        SetStatus(SyncStatus.Syncing);

        try
        {
            // 1. Push pending local changes to server
            await PushPendingChangesAsync();

            // 2. Pull server changes since last sync
            await PullServerChangesAsync();

            await _storage.SetMetaAsync("lastSyncAt", DateTime.UtcNow.ToString("O"));
            PendingCount = await _storage.GetSyncQueueCountAsync();
            SetStatus(PendingCount > 0 ? SyncStatus.Pending : SyncStatus.Synced);

            _logger.LogInformation("Sync completed. {Pending} items still pending.", PendingCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed");
            SetStatus(SyncStatus.Error);
        }
    }

    private async Task PushPendingChangesAsync()
    {
        var pending = await _storage.GetPendingSyncItemsAsync();
        if (!pending.Any()) return;

        var deviceId = await _storage.GetMetaAsync("deviceId") ?? Guid.NewGuid().ToString();

        var pushDto = new
        {
            DeviceId = deviceId,
            ClientTime = DateTime.UtcNow,
            Operations = pending
        };

        var response = await _http.PostAsJsonAsync("/api/sync/push", pushDto);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<SyncPushResult>();
            if (result?.Results != null)
            {
                foreach (var r in result.Results)
                {
                    if (r.Success)
                        await _storage.MarkSyncItemCompleteAsync(r.LocalId, r.ServerId);
                }
            }
        }
    }

    private async Task PullServerChangesAsync()
    {
        var lastSync = await _storage.GetMetaAsync("lastSyncAt");
        var since = lastSync != null ? DateTime.Parse(lastSync) : DateTime.MinValue;

        var response = await _http.GetFromJsonAsync<DataSnapshot>(
            $"/api/sync/pull?since={since:O}");

        if (response != null)
            await _storage.LoadSnapshotAsync(response);
    }

    private async Task LoadFullSnapshotAsync()
    {
        try
        {
            var snapshot = await _http.GetFromJsonAsync<DataSnapshot>("/api/sync/snapshot");
            if (snapshot != null)
                await _storage.LoadSnapshotAsync(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load initial snapshot — starting offline");
        }
    }

    private async Task TrySyncAsync()
    {
        if (await IsOnlineAsync())
            await SyncNowAsync();
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            _isOnline = await _js.InvokeAsync<bool>("eval", "navigator.onLine");
            return _isOnline;
        }
        catch { return false; }
    }

    private void SetStatus(SyncStatus status)
    {
        CurrentStatus = status;
        SyncStatusChanged?.Invoke(status);
    }

    private void UpdateStatus()
    {
        if (!_isOnline) SetStatus(SyncStatus.Offline);
        else if (PendingCount > 0) SetStatus(SyncStatus.Pending);
        else SetStatus(SyncStatus.Synced);
    }

    [JSInvokable("OnOnline")]
    public async Task OnOnlineAsync()
    {
        _isOnline = true;
        _logger.LogInformation("Connection restored — syncing");
        await SyncNowAsync();
    }

    [JSInvokable("OnOffline")]
    public void OnOffline()
    {
        _isOnline = false;
        SetStatus(SyncStatus.Offline);
        _logger.LogWarning("Connection lost — working offline");
    }

    public void Dispose()
    {
        _syncTimer.Dispose();
        _selfRef?.Dispose();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Models
// ─────────────────────────────────────────────────────────────────────────────
public class SyncOperation
{
    public string LocalId { get; set; } = Guid.NewGuid().ToString();
    public string Operation { get; set; } = string.Empty; // CREATE, UPDATE, DELETE
    public string EntityType { get; set; } = string.Empty;
    public Guid? ServerId { get; set; }
    public object? Payload { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Synced { get; set; } = false;
    public int RetryCount { get; set; } = 0;
}

public class SyncPushResult
{
    public List<SyncItemResult> Results { get; set; } = new();
}

public class SyncItemResult
{
    public string LocalId { get; set; } = string.Empty;
    public Guid ServerId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class DataSnapshot
{
    public List<object> Cows { get; set; } = new();
    public List<object> MilkLogs { get; set; } = new();
    public List<object> Sales { get; set; } = new();
    public List<object> Expenses { get; set; } = new();
    public List<object> HealthRecords { get; set; } = new();
    public List<object> Feeds { get; set; } = new();
    public List<object> FeedLogs { get; set; } = new();
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
}
