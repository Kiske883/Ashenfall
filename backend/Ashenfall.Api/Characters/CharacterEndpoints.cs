using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ashenfall.Api.Characters;

public sealed record CreateCharacterRequest(Guid ClassDefinitionId, string Name);
public sealed record SaveCharacterRequest(long ExpectedRevision, long Experience,
    int AllocatedStrength, int AllocatedDexterity, int AllocatedVitality);
public sealed record EquipmentRequest(Guid OwnedItemId, string Slot, long ExpectedRevision);

/// <summary>Explicit local-only identity boundary, disabled until a private token is configured.</summary>
public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapDevelopmentCharacterEndpoints(this IEndpointRouteBuilder routes,
        IWebHostEnvironment environment, IConfiguration configuration)
    {
        string? token = configuration["ASHENFALL_DEV_IDENTITY_TOKEN"];
        string? subject = configuration["ASHENFALL_DEV_IDENTITY_SUBJECT"];
        if (!environment.IsDevelopment() || configuration["ASHENFALL_CHARACTERS_ENABLED"] != "1" ||
            string.IsNullOrEmpty(token) || token.Length < 32 ||
            string.IsNullOrWhiteSpace(subject)) return routes;
        var group = routes.MapGroup("/api/v1/dev/character");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!IPAddress.IsLoopback(http.Connection.RemoteIpAddress ?? IPAddress.None))
                return Results.NotFound();
            string supplied = http.Request.Headers["X-Ashenfall-Dev-Token"].ToString();
            byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                return Results.Unauthorized();
            return await next(context);
        });
        group.MapGet("/", async (CharacterService service, CancellationToken ct) =>
        {
            var account = await service.FindAccountAsync("local_development", subject, ct);
            return account is null ? Results.NotFound() : Results.Ok(await service.GetActiveAsync(account.Id, ct));
        });
        group.MapGet("/gameplay-snapshot", async (CharacterDbContext db, CancellationToken ct) =>
        {
            try
            {
                var v2 = await new GameplaySnapshotV2Reader(db).ReadAsync("local_development", subject, ct);
                if (v2.IsV2) return v2.Snapshot is null ? Results.NotFound() : Results.Ok(v2.Snapshot);
                var snapshot = await new CharacterGameplaySnapshotReader(db).ReadAsync("local_development", subject, ct);
                return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
            }
            catch (InvalidGameplaySnapshotException) { return Results.Problem(statusCode: 500, title: "Invalid character gameplay data."); }
            catch (NpgsqlException) { return Results.Problem(statusCode: 503, title: "Character data unavailable."); }
            catch (TimeoutException) { return Results.Problem(statusCode: 503, title: "Character data unavailable."); }
            catch (InvalidOperationException e) when (e.InnerException is NpgsqlException)
            { return Results.Problem(statusCode: 503, title: "Character data unavailable."); }
            catch (InvalidOperationException) { return Results.Problem(statusCode: 500, title: "Invalid character gameplay data."); }
        });
        group.MapPost("/", async (CreateCharacterRequest request, CharacterService service, CancellationToken ct) =>
            await Safe(async () =>
            {
                var account = await service.GetOrCreateAccountAsync("local_development", subject, ct);
                return Results.Ok(await service.CreateAsync(account.Id, request.ClassDefinitionId, request.Name, ct));
            }));
        group.MapPut("/", async (SaveCharacterRequest request, CharacterService service, CancellationToken ct) =>
            await Safe(async () =>
            {
                var account = await service.FindAccountAsync("local_development", subject, ct)
                    ?? throw new ArgumentException("Account not found.");
                var current = await service.GetActiveAsync(account.Id, ct)
                    ?? throw new ArgumentException("Character not found.");
                if (request.Experience != current.Experience)
                    throw new ArgumentException("Experience is controlled by the server.");
                return Results.Ok(await service.SaveAsync(account.Id, request.ExpectedRevision,
                    request.Experience, request.AllocatedStrength,
                    request.AllocatedDexterity, request.AllocatedVitality, ct));
            }));
        group.MapDelete("/", async (long expectedRevision, CharacterService service, CancellationToken ct) =>
            await Safe(async () =>
            {
                var account = await service.FindAccountAsync("local_development", subject, ct)
                    ?? throw new ArgumentException("Account not found.");
                await service.SoftDeleteAsync(account.Id, expectedRevision, ct);
                return Results.NoContent();
            }));
        group.MapGet("/inventory", async (CharacterService service, CancellationToken ct) =>
        {
            var account = await service.FindAccountAsync("local_development", subject, ct);
            return account is null ? Results.NotFound() : Results.Ok(await service.InventoryAsync(account.Id, ct));
        });
        group.MapGet("/stats", async (CharacterService service, CancellationToken ct) =>
        {
            var account = await service.FindAccountAsync("local_development", subject, ct);
            return account is null ? Results.NotFound() : Results.Ok(await service.EffectiveStatsAsync(account.Id, ct));
        });
        group.MapGet("/equipment", async (CharacterService service, CancellationToken ct) =>
        {
            var account = await service.FindAccountAsync("local_development", subject, ct);
            return account is null ? Results.NotFound() : Results.Ok(await service.EquipmentAsync(account.Id, ct));
        });
        group.MapPut("/equipment", async (EquipmentRequest request, CharacterService service, CancellationToken ct) =>
            await Safe(async () =>
            {
                var account = await service.FindAccountAsync("local_development", subject, ct)
                    ?? throw new ArgumentException("Account not found.");
                await service.EquipAsync(account.Id, request.OwnedItemId, request.Slot, request.ExpectedRevision, ct);
                return Results.NoContent();
            }));
        group.MapDelete("/equipment/{slot}", async (string slot, long expectedRevision,
            CharacterService service, CancellationToken ct) => await Safe(async () =>
        {
            var account = await service.FindAccountAsync("local_development", subject, ct)
                ?? throw new ArgumentException("Account not found.");
            await service.UnequipAsync(account.Id, slot, expectedRevision, ct);
            return Results.NoContent();
        }));
        return routes;
    }

    private static async Task<IResult> Safe(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException) { return Results.Problem(statusCode: 400, title: "Invalid character request."); }
        catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "Stale character revision."); }
        catch (InvalidOperationException) { return Results.Problem(statusCode: 409, title: "Character state conflict."); }
    }
}
