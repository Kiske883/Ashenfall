using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ashenfall.Api.Catalog.Persistence;

/// <summary>Scaffolds SQL without reading credentials or opening a database connection.</summary>
public sealed class CatalogDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql().Options;
        return new CatalogDbContext(options);
    }
}
