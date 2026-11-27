using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Ashenfall.Api.Characters;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CharacterEndpointSecurityTests
{
    private const string TestToken = "test-only-token-with-more-than-32-characters";

    private sealed class LoopbackFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                context.Connection.RemoteIpAddress = context.Request.Headers.ContainsKey("X-Test-Remote")
                    ? IPAddress.Parse("192.0.2.1") : IPAddress.Loopback;
                return following();
            });
            next(app);
        };
    }

    private sealed class GatedFactory(bool development = true) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(development ? "Development" : "Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ASHENFALL_DB_CONNECTION"] = "Host=invalid.local;Database=not_used;Username=ashenfall_app",
                    ["ASHENFALL_CHARACTERS_ENABLED"] = "1",
                    ["ASHENFALL_DEV_IDENTITY_TOKEN"] = TestToken,
                    ["ASHENFALL_DEV_IDENTITY_SUBJECT"] = "test-subject"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CharacterDbContext>>();
                services.RemoveAll<CharacterDbContext>();
                services.AddDbContext<CharacterDbContext>(o => o.UseNpgsql("Host=127.0.0.1;Port=1;Database=not_used;Username=ashenfall_app;Timeout=1"));
                services.AddTransient<IStartupFilter, LoopbackFilter>();
            });
        }
    }

    [Fact]
    public async Task LocalDevelopmentEndpointRejectsAbsentWrongAndNonlocalTokensBeforeDatabaseUse()
    {
        using var factory = new GatedFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/dev/character/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/dev/character/gameplay-snapshot")).StatusCode);
        using var wrong = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dev/character/");
        wrong.Headers.Add("X-Ashenfall-Dev-Token", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrong)).StatusCode);
        using var remote = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dev/character/");
        remote.Headers.Add("X-Ashenfall-Dev-Token", TestToken);
        remote.Headers.Add("X-Test-Remote", "1");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(remote)).StatusCode);
        using var remoteSnapshot = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dev/character/gameplay-snapshot");
        remoteSnapshot.Headers.Add("X-Ashenfall-Dev-Token", TestToken);
        remoteSnapshot.Headers.Add("X-Test-Remote", "1");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(remoteSnapshot)).StatusCode);
    }

    [Fact]
    public async Task GameplaySnapshotIsNotMappedOutsideDevelopment()
    {
        using var factory = new GatedFactory(false);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dev/character/gameplay-snapshot");
        request.Headers.Add("X-Ashenfall-Dev-Token", TestToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task GameplaySnapshotDatabaseFailureIsControlled()
    {
        using var factory = new GatedFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dev/character/gameplay-snapshot");
        request.Headers.Add("X-Ashenfall-Dev-Token", TestToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("127.0.0.1", await response.Content.ReadAsStringAsync());
    }
}
