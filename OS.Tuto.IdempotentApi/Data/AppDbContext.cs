using Microsoft.EntityFrameworkCore;
using OS.Tuto.IdempotentApi.Domain;
using OS.Tuto.IdempotentApi.Idempotency;

namespace OS.Tuto.IdempotentApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Amount).HasPrecision(18, 2);
            b.HasIndex(p => p.IdempotencyKey).IsUnique(); // Safety guard
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Key).IsUnique();
            b.Property(x => x.ResponseBody).HasColumnType("TEXT"); // JSON string
        });
    }
}
