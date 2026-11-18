using CorporateHRBot.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorporateHRBot.Infrastructure;

public class HrRagDbContext : DbContext
{
    public HrRagDbContext(DbContextOptions<HrRagDbContext> options) : base(options) { }

    public DbSet<DocumentChunkEntity> DocumentChunks => Set<DocumentChunkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Настройка ограничений и индексов при создании таблиц
        modelBuilder.Entity<DocumentChunkEntity>()
            .HasIndex(d => d.FileName);

        modelBuilder.Entity<DocumentChunkEntity>()
            .Property(d => d.FileName)
            .HasMaxLength(255);
    }
}
