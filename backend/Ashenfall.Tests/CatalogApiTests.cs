using System.Net;
using System.Net.Http.Json;
using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Catalog.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CatalogApiTests
{
    [Fact]
    public async Task VersionAndSnapshotRespectApprovedRoutesAndEtag()
    {
        using var factory = new CatalogFactory(new FakeReader());
        using var client = factory.CreateClient();
        var version = await client.GetAsync("/api/v1/catalog/version");
        Assert.Equal(HttpStatusCode.OK, version.StatusCode);
        var versionBody = await version.Content.ReadFromJsonAsync<VersionDto>();
        Assert.Equal(InitialCatalogSeed.VersionKey, versionBody!.CatalogVersion);

        var response = await client.GetAsync($"/api/v1/catalog/snapshot?version={InitialCatalogSeed.VersionKey}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        var body = await response.Content.ReadFromJsonAsync<SnapshotDto>();
        Assert.Single(body!.Classes);
        Assert.Single(body.Enemies);
        Assert.Equal(2, body.Items.Length);
        using var conditional = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/catalog/snapshot?version={InitialCatalogSeed.VersionKey}");
        conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(conditional)).StatusCode);
    }

    [Fact]
    public async Task HistoricalAndUnavailableContracts()
    {
        using var factory = new CatalogFactory(new FakeReader());
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/v1/catalog/snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/catalog/snapshot?version=missing")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/catalog/snapshot?version=retired-1")).StatusCode);

        using var unavailable = new CatalogFactory(new UnconfiguredCatalogReader());
        using var unavailableClient = unavailable.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await unavailableClient.GetAsync("/api/v1/catalog/version")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await unavailableClient.GetAsync("/api/v1/catalog/snapshot?version=prototype-1")).StatusCode);

        using var empty = new CatalogFactory(new EmptyReader());
        using var emptyClient = empty.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await emptyClient.GetAsync("/api/v1/catalog/version")).StatusCode);
    }

    [Fact]
    public async Task UnexpectedReaderFailureReturnsSafePublicUnavailableResponse()
    {
        using var factory = new CatalogFactory(new ThrowingReader());
        using var client = factory.CreateClient();
        var version = await client.GetAsync("/api/v1/catalog/version");
        var snapshot = await client.GetAsync("/api/v1/catalog/snapshot?version=prototype-1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, version.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, snapshot.StatusCode);
        Assert.DoesNotContain("private diagnostic detail", await version.Content.ReadAsStringAsync());
        Assert.DoesNotContain("private diagnostic detail", await snapshot.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DevelopmentCharacterRoutesAreAbsentWithoutExplicitLocalIdentityGate()
    {
        using var factory = new CatalogFactory(new FakeReader());
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/dev/character/")).StatusCode);
    }

    private sealed class ThrowingReader : ICatalogReader
    {
        public Task<CatalogRelease?> GetActiveReleaseAsync(CancellationToken _) =>
            throw new InvalidOperationException("private diagnostic detail");

        public Task<CatalogSnapshot?> GetSnapshotAsync(string version, CancellationToken _) =>
            throw new InvalidOperationException("private diagnostic detail");
    }

    private sealed class CatalogFactory(ICatalogReader reader) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICatalogReader>();
            services.AddSingleton(reader);
        });
    }

    private sealed class FakeReader : ICatalogReader
    {
        public Task<CatalogRelease?> GetActiveReleaseAsync(CancellationToken _) =>
            Task.FromResult<CatalogRelease?>(Published().Release);

        public Task<CatalogSnapshot?> GetSnapshotAsync(string version, CancellationToken _)
        {
            if (version is not (InitialCatalogSeed.VersionKey or "retired-1"))
                return Task.FromResult<CatalogSnapshot?>(null);
            var snapshot = Published();
            if (version == "retired-1")
            {
                snapshot.Release.VersionKey = version;
                snapshot.Release.Status = CatalogStates.Retired;
                snapshot.Release.ContentSha256 = CatalogHash.Compute(snapshot);
            }
            return Task.FromResult<CatalogSnapshot?>(snapshot);
        }

        private static CatalogSnapshot Published()
        {
            var snapshot = InitialCatalogSeed.CreateDraft();
            snapshot.Release.Status = CatalogStates.Published;
            snapshot.Release.PublishedAt = DateTimeOffset.UnixEpoch;
            snapshot.Release.ContentSha256 = CatalogHash.Compute(snapshot);
            return snapshot;
        }
    }

    private sealed class EmptyReader : ICatalogReader
    {
        public Task<CatalogRelease?> GetActiveReleaseAsync(CancellationToken _) => Task.FromResult<CatalogRelease?>(null);
        public Task<CatalogSnapshot?> GetSnapshotAsync(string _, CancellationToken cancellationToken) =>
            Task.FromResult<CatalogSnapshot?>(null);
    }

    private sealed record VersionDto(string CatalogVersion);
    private sealed record SnapshotDto(object[] Classes, object[] Enemies, object[] Items);
}
