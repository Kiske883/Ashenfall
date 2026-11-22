#nullable enable
using System;
using HttpClient = System.Net.Http.HttpClient;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Ashenfall.Catalog;

/// <summary>Non-blocking catalog bridge; new catalog encounters pin its validated snapshot.</summary>
public partial class CatalogRuntime : Node
{
    private HttpClient? _http;
    private CatalogService? _service;
    private CancellationTokenSource? _lifetime;
    private string? _configurationError;
    private int _reloadRequestCount;

    public CatalogSnapshot? ActiveSnapshot => _service?.ActiveSnapshot;
    public bool IsReady => _service?.IsReady ?? false;
    public CatalogLoadState State => _service?.State ??
        (_configurationError is null ? CatalogLoadState.NotStarted : CatalogLoadState.Failed);
    public string? LastError => _service?.LastError ?? _configurationError;
    public int ReloadRequestCount => Volatile.Read(ref _reloadRequestCount);

    public override void _Ready()
    {
        _lifetime = new CancellationTokenSource();
        try
        {
            // Verified ASP.NET Core no-profile default. Override per machine with this environment variable.
            string url = System.Environment.GetEnvironmentVariable("ASHENFALL_CATALOG_API_URL") ?? "http://localhost:5000";
            _http = new HttpClient();
            _service = new CatalogService(new CatalogApiClient(_http, CatalogApiClient.ParseBaseUri(url),
                TimeSpan.FromSeconds(5)));
            _ = LoadAsync("startup");
        }
        catch (CatalogLoadException error)
        {
            _configurationError = error.Message;
            _http?.Dispose();
            GD.PushWarning($"Catalog startup failed: {error.Message}. Catalog-dependent scenes wait for retry.");
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        // F9 is Godot's embedded-game Pause shortcut. Use a game-owned chord instead.
        if (@event is InputEventKey key && key.Pressed && !key.Echo &&
            key.Keycode == Key.R && key.ShiftPressed &&
            !key.CtrlPressed && !key.AltPressed && !key.MetaPressed)
        {
            Interlocked.Increment(ref _reloadRequestCount);
            _ = LoadAsync("Shift+R");
            GetViewport().SetInputAsHandled();
        }
    }

    public Task<CatalogReloadResult> ReloadAsync(CancellationToken cancellationToken = default) =>
        _service?.ReloadAsync(cancellationToken) ?? Task.FromResult(CatalogReloadResult.Failed);

    private async Task LoadAsync(string source)
    {
        if (_service is null || _lifetime is null) return;
        int operationId = Interlocked.Increment(ref _nextOperationId);
        GD.Print($"Catalog reload requested [{operationId}, {source}, {Time.GetTicksMsec()} ms]: " +
            $"paused={GetTree().Paused}, time_scale={Engine.TimeScale}, mouse_mode={Input.MouseMode}.");
        var result = await _service.ReloadAsync(_lifetime.Token);
        if (!IsInsideTree()) return;
        if (result == CatalogReloadResult.Ready)
            GD.Print($"Catalog reload succeeded [{operationId}, {Time.GetTicksMsec()} ms]: " +
                $"version={ActiveSnapshot?.VersionKey}. Active encounters remain pinned.");
        else if (result == CatalogReloadResult.Failed)
            GD.PushWarning($"Catalog reload failed [{operationId}, {Time.GetTicksMsec()} ms]: " +
                $"{LastError} Active encounters retain their pinned statistics.");
        else if (result == CatalogReloadResult.Busy)
            GD.Print($"Catalog reload ignored [{operationId}, {Time.GetTicksMsec()} ms]: already in progress.");
        else
            GD.Print($"Catalog reload cancelled [{operationId}, {Time.GetTicksMsec()} ms].");
    }

    private int _nextOperationId;

    public override void _ExitTree()
    {
        _lifetime?.Cancel();
        _http?.Dispose();
        _lifetime?.Dispose();
    }
}
