#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ashenfall.Characters;

public sealed record PersistedCharacter(Guid Id, Guid ClassDefinitionId, string CharacterName,
    long Experience, int AllocatedStrength, int AllocatedDexterity, int AllocatedVitality, long RowRevision);
public sealed record CharacterGameplayStats(int Level, int MaxHealth, int FixedDamage,
    int? MinDamage = null, int? MaxDamage = null, double? CriticalChancePercent = null,
    double? CriticalDamageBonusPercent = null);
public sealed record PersistedGameplaySnapshot(int SchemaVersion, Guid CharacterId, long CharacterRevision,
    Guid ClassDefinitionId, Guid CatalogReleaseId, string CatalogVersion, string CatalogHash, CharacterGameplayStats EffectiveStats);

/// <summary>Read-only development character transport, with no Godot or database dependency.</summary>
public sealed class PersistedCharacterClient(HttpClient http)
{
    /// <summary>Owned by the caller. Injected test/specialized transports must also disable automatic redirects.</summary>
    public static HttpClient CreateHttpClient() => new(new HttpClientHandler { AllowAutoRedirect = false });

    private static void RejectRedirect(HttpResponseMessage response)
    {
        if ((int)response.StatusCode is >= 300 and < 400)
            throw new InvalidOperationException("Character API redirects are not allowed.");
    }

    public async Task<PersistedGameplaySnapshot> LoadSnapshotAsync(Uri endpoint, string token, Guid expectedId, CancellationToken cancel = default)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttp || !endpoint.IsLoopback || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/" || expectedId == Guid.Empty || string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("A local API URL, character ID and development token are required.");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "api/v1/dev/character/gameplay-snapshot"));
        request.Headers.Add("X-Ashenfall-Dev-Token", token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            RejectRedirect(response);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) throw new InvalidOperationException("Character API authentication failed.");
            if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("Configured character was not found or preview access is disabled.");
            if ((int)response.StatusCode >= 500) throw new InvalidOperationException("Character database service is unavailable or returned invalid data.");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Character API returned HTTP {(int)response.StatusCode}.");
            if (response.Content.Headers.ContentLength is > 65536) throw new InvalidOperationException("Snapshot response is too large.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096]; int count;
            while ((count = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + count > 65536) throw new InvalidOperationException("Snapshot response is too large.");
                buffer.Write(chunk, 0, count);
            }
            try {
            using var doc = JsonDocument.Parse(buffer.ToArray());
            var root = doc.RootElement;
            var stats = root.GetProperty("effectiveStats");
            int schema = root.GetProperty("schemaVersion").GetInt32();
            var id = root.GetProperty("characterId").GetGuid();
            long revision = root.GetProperty("characterRevision").GetInt64();
            var classId = root.GetProperty("classDefinitionId").GetGuid();
            var releaseId = root.GetProperty("catalogReleaseId").GetGuid();
            var version = root.GetProperty("catalogVersion").GetString();
            var hash = root.GetProperty("catalogHash").GetString();
            var level = stats.GetProperty("level").GetInt32();
            var health = stats.GetProperty("maxHealth").GetInt32();
            var damage = stats.GetProperty("fixedDamage").GetInt32();
            if ((schema != 1 && schema != 2) || id != expectedId || id == Guid.Empty || revision < 1 || classId == Guid.Empty || releaseId == Guid.Empty ||
                string.IsNullOrWhiteSpace(version) || version.Length > 64 || hash is null || hash.Length != 64 ||
                !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-f]{64}$") || level < 1 || health is < 1 or > 10000 || damage is < 0 or > 1000)
                throw new InvalidOperationException("Character snapshot failed validation.");
            int? min = null, max = null; double? chance = null, bonus = null;
            if (schema == 2)
            {
                min = stats.GetProperty("minDamage").GetInt32();
                max = stats.GetProperty("maxDamage").GetInt32();
                chance = stats.GetProperty("criticalChancePercent").GetDouble();
                bonus = stats.GetProperty("criticalDamageBonusPercent").GetDouble();
                if (min < 0 || max < min || max >= int.MaxValue || !double.IsFinite(chance.Value) || chance < 0 || chance > 100 || !double.IsFinite(bonus.Value) || bonus < 0 ||
                    max.Value * (1 + bonus.Value / 100) > int.MaxValue)
                    throw new InvalidOperationException("Invalid combat statistics.");
            }
            return new PersistedGameplaySnapshot(schema, id, revision, classId, releaseId, version, hash, new(level, health, damage, min, max, chance, bonus));
            } catch (Exception e) when (e is JsonException or KeyNotFoundException or FormatException or OverflowException or InvalidOperationException) { throw new InvalidOperationException("Character API returned an invalid snapshot."); }
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested) { throw new InvalidOperationException("Character API request timed out."); }
        catch (HttpRequestException) { throw new InvalidOperationException("Character API is unavailable."); }
    }
    public async Task<PersistedCharacter> LoadAsync(Uri endpoint, string token, Guid expectedId, CancellationToken cancel = default)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttp || !endpoint.IsLoopback || endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/")
            throw new InvalidOperationException("Character API must use a local HTTP base URL.");
        if (string.IsNullOrWhiteSpace(token) || expectedId == Guid.Empty)
            throw new InvalidOperationException("Character ID and development token are required.");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "api/v1/dev/character/"));
        request.Headers.Add("X-Ashenfall-Dev-Token", token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            RejectRedirect(response);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("Configured character was not found.");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Character API returned HTTP {(int)response.StatusCode}.");
            if (response.Content.Headers.ContentLength is > 65536) throw new InvalidOperationException("Character response is too large.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + count > 65536) throw new InvalidOperationException("Character response is too large.");
                buffer.Write(chunk, 0, count);
            }
            var value = JsonSerializer.Deserialize<PersistedCharacter>(buffer.ToArray(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (value is null || value.Id != expectedId || value.ClassDefinitionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(value.CharacterName) || value.RowRevision < 1 || value.Experience < 0)
                throw new InvalidOperationException("Character response is missing, invalid, or does not match the configured ID.");
            return value;
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested) { throw new InvalidOperationException("Character API request timed out."); }
        catch (HttpRequestException) { throw new InvalidOperationException("Character API is unavailable."); }
        catch (JsonException) { throw new InvalidOperationException("Character API returned invalid data."); }
    }
}
