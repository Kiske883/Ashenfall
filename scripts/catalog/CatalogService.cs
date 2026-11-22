#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ashenfall.Catalog;

public enum CatalogLoadState { NotStarted, Loading, Ready, Failed }
public enum CatalogReloadResult { Ready, Failed, Cancelled, Busy }

/// <summary>Publishes only fully validated immutable snapshots; consumers pin the reference they obtain.</summary>
public sealed class CatalogService(CatalogApiClient client)
{
    private CatalogSnapshot? _active;
    private int _state;
    private int _loading;
    private string? _lastError;

    public CatalogSnapshot? ActiveSnapshot => Volatile.Read(ref _active);
    public bool IsReady => ActiveSnapshot is not null;
    public CatalogLoadState State => (CatalogLoadState)Volatile.Read(ref _state);
    public string? LastError => Volatile.Read(ref _lastError);

    public async Task<CatalogReloadResult> ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0)
            return CatalogReloadResult.Busy;
        Volatile.Write(ref _state, (int)CatalogLoadState.Loading);
        Volatile.Write(ref _lastError, null);
        try
        {
            // A Task-returning HTTP handler may still run synchronously until its first await.
            // Keep that work and canonical hashing off Godot's input/render thread.
            var candidate = await Task.Run(async () =>
            {
                var version = await client.GetVersionAsync(cancellationToken);
                if (version.SchemaMajor != 1)
                    throw new CatalogLoadException("Unsupported catalog schema version.");
                var snapshot = await client.GetSnapshotAsync(version.VersionKey, cancellationToken);
                CatalogCodec.Verify(version, snapshot);
                return snapshot;
            }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _active, candidate);
            Volatile.Write(ref _state, (int)CatalogLoadState.Ready);
            return CatalogReloadResult.Ready;
        }
        catch (OperationCanceledException)
        {
            Volatile.Write(ref _lastError, "Catalog loading was cancelled.");
            Volatile.Write(ref _state, (int)CatalogLoadState.Failed);
            return CatalogReloadResult.Cancelled;
        }
        catch (CatalogLoadException error)
        {
            Volatile.Write(ref _lastError, error.Message);
            Volatile.Write(ref _state, (int)CatalogLoadState.Failed);
            return CatalogReloadResult.Failed;
        }
        catch (Exception)
        {
            // Do not surface transport exception text, which can contain configured URLs.
            Volatile.Write(ref _lastError, "Catalog loading failed unexpectedly.");
            Volatile.Write(ref _state, (int)CatalogLoadState.Failed);
            return CatalogReloadResult.Failed;
        }
        finally { Interlocked.Exchange(ref _loading, 0); }
    }
}
