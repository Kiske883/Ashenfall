#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ashenfall.Catalog;

/// <summary>HTTP-only transport. No PostgreSQL or Godot dependency.</summary>
public sealed class CatalogApiClient(HttpClient http, Uri baseUri, TimeSpan requestTimeout)
{
    public async Task<CatalogVersion> GetVersionAsync(CancellationToken cancellationToken = default) =>
        CatalogCodec.ParseVersion(await GetJsonAsync("api/v1/catalog/version", cancellationToken));

    public async Task<CatalogSnapshot> GetSnapshotAsync(string versionKey, CancellationToken cancellationToken = default) =>
        CatalogCodec.ParseSnapshot(await GetJsonAsync(
            "api/v1/catalog/snapshot?version=" + Uri.EscapeDataString(versionKey), cancellationToken));

    private async Task<string> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        try
        {
            using var response = await http.GetAsync(new Uri(baseUri, path),
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new CatalogLoadException($"Catalog API returned HTTP {(int)response.StatusCode}.");
            if (response.Content.Headers.ContentLength is > 1_048_576)
                throw new CatalogLoadException("Catalog response exceeds the 1 MiB limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (bounded.Length + read > 1_048_576)
                    throw new CatalogLoadException("Catalog response exceeds the 1 MiB limit.");
                bounded.Write(buffer, 0, read);
            }
            return new System.Text.UTF8Encoding(false, true).GetString(bounded.ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new CatalogLoadException("Catalog API request timed out."); }
        catch (HttpRequestException)
        { throw new CatalogLoadException("Catalog API is unavailable."); }
        catch (System.Text.DecoderFallbackException)
        { throw new CatalogLoadException("Catalog API response is not valid UTF-8."); }
    }

    public static Uri ParseBaseUri(string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new CatalogLoadException("Catalog API base URL must be an HTTP(S) URL without credentials or query.");
        return uri;
    }
}
