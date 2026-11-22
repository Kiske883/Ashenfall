using Ashenfall.Api.Catalog.Endpoints;
using Ashenfall.Api.Catalog.Persistence;
using Ashenfall.Api.Catalog.Services;
using Ashenfall.Api.Characters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration["ASHENFALL_DB_CONNECTION"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton<ICatalogReader, UnconfiguredCatalogReader>();
}
else
{
    NpgsqlConnectionStringBuilder parsed;
    try { parsed = new NpgsqlConnectionStringBuilder(connectionString); }
    catch (ArgumentException) { throw new InvalidOperationException("Invalid catalog database configuration."); }
    if (!string.Equals(parsed.Username, "ashenfall_app", StringComparison.Ordinal))
        throw new InvalidOperationException("The catalog API requires the restricted application role.");
    builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddDbContext<CharacterDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<CharacterService>();
    builder.Services.AddScoped<ICatalogReader, CatalogReader>();
}

var app = builder.Build();
if (string.IsNullOrWhiteSpace(connectionString))
    app.Logger.LogWarning("Catalog database configuration is missing; catalog endpoints will return 503. Set ASHENFALL_DB_CONNECTION through Development user secrets or the process environment.");
else if (app.Environment.IsDevelopment())
    app.Logger.LogInformation("Catalog database configuration is present for the restricted runtime role.");
app.MapCatalogEndpoints();
if (!string.IsNullOrWhiteSpace(connectionString))
    app.MapDevelopmentCharacterEndpoints(app.Environment, app.Configuration);
app.Run();

public partial class Program { }
