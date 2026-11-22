using Ashenfall.CatalogPublisher;
using Npgsql;

if (args.Length != 1 || args[0] != "publish-initial")
{
    Console.Error.WriteLine("Usage: Ashenfall.CatalogPublisher publish-initial");
    return 2;
}

try
{
    var configured = Environment.GetEnvironmentVariable("ASHENFALL_PUBLISHER_CONNECTION");
    var settings = string.IsNullOrWhiteSpace(configured)
        ? new NpgsqlConnectionStringBuilder
        {
            Host = "localhost", Port = 5432, Database = "ashenfall_dev",
            Username = "ashenfall_publisher", Pooling = false
        }
        : new NpgsqlConnectionStringBuilder(configured);
    if (settings.Database != "ashenfall_dev" || settings.Username != "ashenfall_publisher" ||
        !string.IsNullOrEmpty(settings.Password))
        throw new InvalidOperationException("Publisher requires the approved database and role, with credentials supplied privately by libpq/pgpass.");
    var result = await CatalogPublication.PublishInitialAsync(settings.ConnectionString);
    Console.WriteLine(result == PublicationResult.Published
        ? "Published prototype-1."
        : "prototype-1 is already published with the approved content; no changes made.");
    return 0;
}
catch (Exception error)
{
    // Npgsql exception messages may contain connection configuration; never echo them.
    Console.Error.WriteLine($"Publication failed ({error.GetType().Name}). No partial publication was committed.");
    return 1;
}
