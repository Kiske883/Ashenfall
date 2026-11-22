using Ashenfall.Api.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ashenfall.Api.Catalog.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<CatalogRelease> Releases => Set<CatalogRelease>();
    public DbSet<CharacterClass> Classes => Set<CharacterClass>();
    public DbSet<EnemyDefinition> Enemies => Set<EnemyDefinition>();
    public DbSet<ItemDefinition> Items => Set<ItemDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CatalogReleaseConfiguration());
        modelBuilder.ApplyConfiguration(new CharacterClassConfiguration());
        modelBuilder.ApplyConfiguration(new EnemyDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new ItemDefinitionConfiguration());
    }
}
