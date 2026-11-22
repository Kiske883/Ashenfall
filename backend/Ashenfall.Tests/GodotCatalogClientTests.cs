using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Catalog;
using Xunit;
using GameSnapshot = Ashenfall.Catalog.CatalogSnapshot;

namespace Ashenfall.Tests;

public sealed class GodotCatalogClientTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Uri BaseUri = CatalogApiClient.ParseBaseUri("http://localhost:5000");

    private static (string Version, string Snapshot) Responses(string versionKey = "prototype-1")
    {
        var seed = InitialCatalogSeed.CreateDraft();
        seed.Release.VersionKey = versionKey;
        string hash = Ashenfall.Api.Catalog.Services.CatalogHash.Compute(seed);
        string version = JsonSerializer.Serialize(new
        {
            schemaMajor = 1, catalogVersion = seed.Release.VersionKey,
            contentSha256 = hash, publishedAt = DateTimeOffset.UtcNow
        }, Json);
        string snapshot = JsonSerializer.Serialize(new
        {
            schemaMajor = 1, catalogVersion = seed.Release.VersionKey,
            contentSha256 = hash, classes = seed.Classes, enemies = seed.Enemies, items = seed.Items
        }, Json);
        return (version, snapshot);
    }

    private static CatalogService Service(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        TimeSpan? timeout = null) => new(new CatalogApiClient(
            new HttpClient(new Handler(send)), BaseUri, timeout ?? TimeSpan.FromSeconds(2)));

    private static Task<HttpResponseMessage> Respond(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        Task.FromResult(new HttpResponseMessage(code)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    private static CatalogService HealthyService((string Version, string Snapshot) data) =>
        Service((request, _) => Respond(request.RequestUri!.AbsolutePath.EndsWith("version")
            ? data.Version : data.Snapshot));

    [Fact]
    public void ClientParsesAndVerifiesApprovedCrossImplementationVector()
    {
        var data = Responses();
        var version = CatalogCodec.ParseVersion(data.Version);
        GameSnapshot snapshot = CatalogCodec.ParseSnapshot(data.Snapshot);
        CatalogCodec.Verify(version, snapshot);
        Assert.Equal("a333aa3843e3f8dc44b11352b3d15ba37c256f6b5ab12594eb34b40421a7ef17",
            CatalogContentHash.Compute(snapshot));
        Assert.Single(snapshot.Classes);
        Assert.Single(snapshot.Enemies);
        Assert.Equal(2, snapshot.Items.Length);
    }

    [Fact]
    public void DefinitionOrderAndDecimalScaleDoNotChangeHash()
    {
        var data = Responses();
        var original = CatalogCodec.ParseSnapshot(data.Snapshot);
        var changed = JsonNode.Parse(data.Snapshot)!.AsObject();
        var items = changed["items"]!.AsArray();
        (items[0], items[1]) = (items[1]!.DeepClone(), items[0]!.DeepClone());
        changed["classes"]![0]!["moveSpeedMps"] = 2.500m;
        var reordered = CatalogCodec.ParseSnapshot(changed.ToJsonString());
        Assert.Equal(CatalogContentHash.Compute(original), CatalogContentHash.Compute(reordered));
    }

    [Fact]
    public void ChangedContentAndNullVersusZeroFailVerification()
    {
        var data = Responses();
        var version = CatalogCodec.ParseVersion(data.Version);
        var changed = JsonNode.Parse(data.Snapshot)!.AsObject();
        changed["classes"]![0]!["fixedDamage"] = 26;
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.Verify(version,
            CatalogCodec.ParseSnapshot(changed.ToJsonString())));
        changed = JsonNode.Parse(data.Snapshot)!.AsObject();
        changed["classes"]![0]!["baseStrength"] = 0;
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.Verify(version,
            CatalogCodec.ParseSnapshot(changed.ToJsonString())));
    }

    [Fact]
    public void MissingFieldsAndUnsupportedSchemaAreRejected()
    {
        var data = Responses();
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.ParseSnapshot("{not-json"));
        var missing = JsonNode.Parse(data.Snapshot)!.AsObject();
        missing["enemies"]![0]!.AsObject().Remove("fixedDamage");
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.ParseSnapshot(missing.ToJsonString()));
        var incompatible = JsonNode.Parse(data.Version)!.AsObject();
        incompatible["schemaMajor"] = 2;
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.Verify(
            CatalogCodec.ParseVersion(incompatible.ToJsonString()), CatalogCodec.ParseSnapshot(data.Snapshot)));
        var badHash = JsonNode.Parse(data.Version)!.AsObject();
        badHash["contentSha256"] = "invalid";
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.Verify(
            CatalogCodec.ParseVersion(badHash.ToJsonString()), CatalogCodec.ParseSnapshot(data.Snapshot)));
    }

    [Fact]
    public void MismatchedVersionAndDuplicateIdsAreRejected()
    {
        var data = Responses();
        var version = CatalogCodec.ParseVersion(data.Version);
        var mismatched = JsonNode.Parse(data.Snapshot)!.AsObject();
        mismatched["catalogVersion"] = "another-version";
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.Verify(version,
            CatalogCodec.ParseSnapshot(mismatched.ToJsonString())));
        var duplicate = JsonNode.Parse(data.Snapshot)!.AsObject();
        duplicate["items"]![1]!["id"] = duplicate["items"]![0]!["id"]!.GetValue<string>();
        Assert.Throws<CatalogLoadException>(() => CatalogCodec.Verify(version,
            CatalogCodec.ParseSnapshot(duplicate.ToJsonString())));
    }

    [Fact]
    public async Task LoadIsAtomicAndFailurePreservesPreviousSnapshot()
    {
        var data = Responses();
        bool fail = false;
        var service = Service((request, _) => fail
            ? Respond("", HttpStatusCode.ServiceUnavailable)
            : Respond(request.RequestUri!.AbsolutePath.EndsWith("version") ? data.Version : data.Snapshot));
        Assert.False(service.IsReady);
        Assert.Equal(CatalogReloadResult.Ready, await service.ReloadAsync());
        var pinned = service.ActiveSnapshot;
        Assert.NotNull(pinned);
        fail = true;
        Assert.Equal(CatalogReloadResult.Failed, await service.ReloadAsync());
        Assert.Same(pinned, service.ActiveSnapshot);
        Assert.True(service.IsReady);
        Assert.Equal(CatalogLoadState.Failed, service.State);
        Assert.Contains("503", service.LastError);
    }

    [Fact]
    public async Task SuccessfulReloadAtomicallyReplacesAvailableSnapshot()
    {
        var firstData = Responses();
        var secondData = Responses("prototype-2");
        bool second = false;
        var service = Service((request, _) =>
        {
            var data = second ? secondData : firstData;
            return Respond(request.RequestUri!.AbsolutePath.EndsWith("version")
                ? data.Version : data.Snapshot);
        });
        Assert.Equal(CatalogReloadResult.Ready, await service.ReloadAsync());
        var pinned = service.ActiveSnapshot;
        second = true;
        Assert.Equal(CatalogReloadResult.Ready, await service.ReloadAsync());
        Assert.NotSame(pinned, service.ActiveSnapshot);
        Assert.Equal("prototype-1", pinned!.VersionKey);
        Assert.Equal("prototype-2", service.ActiveSnapshot!.VersionKey);
        Assert.Equal(CatalogLoadState.Ready, service.State);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task HttpErrorsDoNotPublishCatalog(HttpStatusCode status)
    {
        var service = Service((_, _) => Respond("", status));
        Assert.Equal(CatalogReloadResult.Failed, await service.ReloadAsync());
        Assert.Null(service.ActiveSnapshot);
        Assert.Contains(((int)status).ToString(), service.LastError);
    }

    [Fact]
    public async Task NetworkFailureAndTimeoutAreReported()
    {
        var unavailable = Service((_, _) => throw new HttpRequestException());
        Assert.Equal(CatalogReloadResult.Failed, await unavailable.ReloadAsync());
        Assert.Contains("unavailable", unavailable.LastError);
        var timedOut = Service(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, TimeSpan.FromMilliseconds(25));
        Assert.Equal(CatalogReloadResult.Failed, await timedOut.ReloadAsync());
        Assert.Contains("timed out", timedOut.LastError);
    }

    [Fact]
    public async Task CancellationAndConcurrentReloadAreHandled()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var data = Responses();
        var service = Service(async (request, token) =>
        {
            await gate.Task.WaitAsync(token);
            return await Respond(request.RequestUri!.AbsolutePath.EndsWith("version")
                ? data.Version : data.Snapshot);
        });
        using var cancellation = new CancellationTokenSource();
        var first = service.ReloadAsync(cancellation.Token);
        Assert.Equal(CatalogLoadState.Loading, service.State);
        Assert.Equal(CatalogReloadResult.Busy, await service.ReloadAsync());
        cancellation.Cancel();
        Assert.Equal(CatalogReloadResult.Cancelled, await first);
        gate.SetResult(true);
        Assert.Equal(CatalogReloadResult.Ready, await service.ReloadAsync());
    }

    [Fact]
    public async Task ReloadReturnsToCallerBeforeSynchronousTransportWorkCompletes()
    {
        var data = Responses();
        var service = Service((request, _) =>
        {
            // HttpMessageHandler can do synchronous work before yielding a Task.
            Thread.Sleep(300);
            return Respond(request.RequestUri!.AbsolutePath.EndsWith("version")
                ? data.Version : data.Snapshot);
        });
        var clock = Stopwatch.StartNew();
        var pending = service.ReloadAsync();
        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 150,
            $"Reload blocked the caller for {clock.ElapsedMilliseconds} ms before its first await.");
        Assert.Equal(CatalogReloadResult.Ready, await pending);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
