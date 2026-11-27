using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using Ashenfall.Characters;
using Xunit;

namespace Ashenfall.Tests;

public sealed class PersistedCharacterClientTests
{
    [Fact]
    public async Task SnapshotV2_requires_complete_bounded_combat_statistics()
    {
        var id = Guid.NewGuid();
        string payload = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 2, characterId = id, characterRevision = 1, classDefinitionId = Guid.Parse("22222222-2222-4222-8222-222222222222"), catalogReleaseId = Guid.Parse("33333333-3333-4333-8333-333333333333"), catalogVersion = "fixture", catalogHash = new string('a', 64), effectiveStats = new { level = 2, maxHealth = 105, fixedDamage = 27, minDamage = 20, maxDamage = 30, criticalChancePercent = 15, criticalDamageBonusPercent = 150 } });
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) }));
        var client = new PersistedCharacterClient(http);
        var valid = await client.LoadSnapshotAsync(new Uri("http://localhost/"), "test", id);
        Assert.Equal(2, valid.SchemaVersion);
        Assert.Equal(15, valid.EffectiveStats.CriticalChancePercent);
        foreach (var bad in new[] { payload.Replace("\"minDamage\":20,", ""), payload.Replace("\"maxDamage\":30", "\"maxDamage\":19"), payload.Replace("\"criticalChancePercent\":15", "\"criticalChancePercent\":101"), payload.Replace("\"criticalDamageBonusPercent\":150", "\"criticalDamageBonusPercent\":-1") })
        {
            string previous = payload; payload = bad;
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.LoadSnapshotAsync(new Uri("http://localhost/"), "test", id));
            payload = previous;
        }
    }
    [Fact]
    public async Task Snapshot_requires_complete_valid_authoritative_data()
    {
        var id = Guid.NewGuid(); var cls = Guid.NewGuid(); var rel = Guid.NewGuid();
        string json = $"{{\"schemaVersion\":1,\"characterId\":\"{id}\",\"characterRevision\":2,\"classDefinitionId\":\"{cls}\",\"catalogReleaseId\":\"{rel}\",\"catalogVersion\":\"test\",\"catalogHash\":\"{new string('a',64)}\",\"effectiveStats\":{{\"level\":2,\"maxHealth\":105,\"fixedDamage\":27}}}}";
        string payload = json;
        using var http = new HttpClient(new Handler(request => {
            Assert.Equal("/api/v1/dev/character/gameplay-snapshot", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) };
        }));
        var client = new PersistedCharacterClient(http);
        var snapshot = await client.LoadSnapshotAsync(new Uri("http://localhost/"), "token", id);
        Assert.Equal((105, 27), (snapshot.EffectiveStats.MaxHealth, snapshot.EffectiveStats.FixedDamage));
        foreach (var invalid in new[] { "{}", "null", json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), json.Replace("\"fixedDamage\":27", "\"fixedDamage\":-1"), json.Replace(id.ToString(), Guid.NewGuid().ToString()) })
        {
            payload = invalid;
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.LoadSnapshotAsync(new Uri("http://localhost/"), "token", id));
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "authentication")]
    [InlineData(HttpStatusCode.NotFound, "not found")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "service")]
    public async Task Snapshot_errors_are_visible(HttpStatusCode code, string message)
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(code)));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(http).LoadSnapshotAsync(new Uri("http://localhost/"), "token", Guid.NewGuid()));
        Assert.Contains(message, error.Message);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }

    [Fact]
    public async Task Retrieves_only_explicit_character_and_sends_local_development_token()
    {
        var id = Guid.NewGuid(); var classId = Guid.NewGuid();
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("/api/v1/dev/character/", request.RequestUri!.AbsolutePath);
            Assert.Equal("test-token", request.Headers.GetValues("X-Ashenfall-Dev-Token").Single());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"id\":\"{id}\",\"classDefinitionId\":\"{classId}\",\"characterName\":\"Test Dwarf\",\"experience\":2,\"rowRevision\":1}}", Encoding.UTF8, "application/json") };
        }));
        var character = await new PersistedCharacterClient(http).LoadAsync(new Uri("http://localhost:5000/"), "test-token", id);
        Assert.Equal(id, character.Id); Assert.Equal("Test Dwarf", character.CharacterName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(http).LoadAsync(new Uri("http://localhost:5000/"), "test-token", Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(http).LoadAsync(new Uri("http://example.com/"), "test-token", id));
    }

    [Fact]
    public async Task Missing_and_offline_are_visible_errors()
    {
        using var missing = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(missing).LoadAsync(new Uri("http://localhost/"), "token", Guid.NewGuid()));
        Assert.Contains("not found", error.Message);
        using var offline = new HttpClient(new Handler(_ => throw new HttpRequestException("offline")));
        error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(offline).LoadAsync(new Uri("http://localhost/"), "token", Guid.NewGuid()));
        Assert.Contains("unavailable", error.Message);
    }

    [Fact]
    public async Task Offline_loopback_socket_reports_unavailable()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var http = PersistedCharacterClient.CreateHttpClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PersistedCharacterClient(http).LoadAsync(new Uri($"http://127.0.0.1:{port}/"), "token", Guid.NewGuid()));
        Assert.Contains("unavailable", error.Message);
    }

    public static IEnumerable<object[]> RedirectCases()
    {
        foreach (int status in new[] { 301, 302, 303, 307, 308 })
            foreach (string location in new[] { "relative", "same-origin", "cross-origin" })
                foreach (bool snapshot in new[] { false, true })
                    yield return [status, location, snapshot];
    }

    [Theory]
    [MemberData(nameof(RedirectCases))]
    public async Task Real_transport_never_follows_redirect_or_forwards_token(int status, string destination, bool snapshot)
    {
        // Real TCP listeners and the production factory, not a mocked HttpMessageHandler.
        const string syntheticToken = "synthetic-redirect-regression-token";
        var id = Guid.NewGuid();
        await using var otherOrigin = new LoopbackHttpServer(_ => new WireResponse(200, "{}"));
        string? location = null;
        await using var origin = new LoopbackHttpServer(request => request.Path == "/redirect-target" || request.Path == "/listener-probe"
            ? new WireResponse(200, Payload(snapshot, id)) : new WireResponse(status, "", location));
        var target = new Uri(destination == "cross-origin" ? otherOrigin.BaseUri : origin.BaseUri, "redirect-target");
        location = destination == "relative" ? "/redirect-target" : target.AbsoluteUri;
        using var http = PersistedCharacterClient.CreateHttpClient();
        var client = new PersistedCharacterClient(http);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Fetch(client, snapshot, origin.BaseUri, syntheticToken, id));
        Assert.Contains("redirects are not allowed", error.Message);
        Assert.DoesNotContain(syntheticToken, error.Message);
        Assert.DoesNotContain(target.AbsoluteUri, error.Message);
        var initial = Assert.Single(origin.Requests);
        Assert.Equal(snapshot ? "/api/v1/dev/character/gameplay-snapshot" : "/api/v1/dev/character/", initial.Path);
        Assert.Equal(syntheticToken, initial.Token);
        Assert.Empty(otherOrigin.Requests);
        Assert.DoesNotContain(origin.Requests, x => x.Path == "/redirect-target");

        // Positive control: the destination is alive and observable, not an unbound/dead listener.
        using var probe = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        using var response = await probe.GetAsync(new Uri(target, "/listener-probe")).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var destinationRequests = destination == "cross-origin" ? otherOrigin.Requests.ToArray() : origin.Requests.Skip(1).ToArray();
        var probeRequest = Assert.Single(destinationRequests);
        Assert.Equal("/listener-probe", probeRequest.Path);
        Assert.Null(probeRequest.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_transport_successfully_authenticates_over_real_loopback_tcp(bool snapshot)
    {
        const string syntheticToken = "synthetic-success-regression-token";
        var id = Guid.NewGuid();
        await using var server = new LoopbackHttpServer(_ => new WireResponse(200, Payload(snapshot, id)));
        using var http = PersistedCharacterClient.CreateHttpClient();
        await Fetch(new PersistedCharacterClient(http), snapshot, server.BaseUri, syntheticToken, id);
        var request = Assert.Single(server.Requests);
        Assert.Equal(syntheticToken, request.Token);
        Assert.Equal(snapshot ? "/api/v1/dev/character/gameplay-snapshot" : "/api/v1/dev/character/", request.Path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_transport_preserves_initial_url_validation_before_sending(bool snapshot)
    {
        await using var server = new LoopbackHttpServer(_ => new WireResponse(200, "{}"));
        using var http = PersistedCharacterClient.CreateHttpClient();
        var client = new PersistedCharacterClient(http);
        foreach (var uri in new[] { new Uri(server.BaseUri, "unexpected-path/"), new Uri(server.BaseUri + "?query=1"),
            new Uri(server.BaseUri + "#fragment"), new Uri("https://127.0.0.1/"), new Uri("http://example.invalid/") })
            await Assert.ThrowsAsync<InvalidOperationException>(() => Fetch(client, snapshot, uri, "synthetic-token", Guid.NewGuid()));
        Assert.Empty(server.Requests);
    }

    private static async Task Fetch(PersistedCharacterClient client, bool snapshot, Uri endpoint, string token, Guid id)
    {
        if (snapshot) Assert.Equal(id, (await client.LoadSnapshotAsync(endpoint, token, id)).CharacterId);
        else Assert.Equal(id, (await client.LoadAsync(endpoint, token, id)).Id);
    }
    private static string Payload(bool snapshot, Guid id) => snapshot ? JsonSerializer.Serialize(new {
        schemaVersion = 2, characterId = id, characterRevision = 1, classDefinitionId = Guid.NewGuid(), catalogReleaseId = Guid.NewGuid(),
        catalogVersion = "synthetic", catalogHash = new string('a', 64),
        effectiveStats = new { level = 2, maxHealth = 105, fixedDamage = 27, minDamage = 27, maxDamage = 27, criticalChancePercent = 5, criticalDamageBonusPercent = 100 }
    }) : JsonSerializer.Serialize(new { id, classDefinitionId = Guid.NewGuid(), characterName = "Synthetic Dwarf", experience = 100, rowRevision = 1 });

    private sealed record WireRequest(string Path, string? Token);
    private sealed record WireResponse(int Status, string Body, string? Location = null);
    private sealed class LoopbackHttpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new(TimeSpan.FromSeconds(15));
        private readonly Task serving;
        public ConcurrentQueue<WireRequest> Requests { get; } = new();
        public Uri BaseUri { get; }
        public LoopbackHttpServer(Func<WireRequest, WireResponse> respond)
        {
            listener.Start();
            BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
            serving = Serve(respond);
        }
        private async Task Serve(Func<WireRequest, WireResponse> respond)
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    using var socket = await listener.AcceptTcpClientAsync(lifetime.Token);
                    await using var stream = socket.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                    string first = await reader.ReadLineAsync(lifetime.Token) ?? throw new IOException("Missing test request line.");
                    string? token = null;
                    for (int headers = 0; ; headers++)
                    {
                        if (headers > 100) throw new IOException("Too many test headers.");
                        string line = await reader.ReadLineAsync(lifetime.Token) ?? throw new IOException("Incomplete test request.");
                        if (line.Length == 0) break;
                        if (line.StartsWith("X-Ashenfall-Dev-Token:", StringComparison.OrdinalIgnoreCase)) token = line.Split(':', 2)[1].Trim();
                    }
                    var request = new WireRequest(first.Split(' ')[1], token);
                    Requests.Enqueue(request);
                    var response = respond(request);
                    byte[] body = Encoding.UTF8.GetBytes(response.Body);
                    string header = $"HTTP/1.1 {response.Status} Test\r\nContent-Length: {body.Length}\r\nContent-Type: application/json\r\nConnection: close\r\n";
                    if (response.Location is not null) header += "Location: " + response.Location + "\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(header + "\r\n"), lifetime.Token);
                    await stream.WriteAsync(body, lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (SocketException) when (lifetime.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync()
        {
            lifetime.Cancel(); listener.Stop();
            try { await serving.WaitAsync(TimeSpan.FromSeconds(3)); }
            finally { lifetime.Dispose(); }
        }
    }
}
