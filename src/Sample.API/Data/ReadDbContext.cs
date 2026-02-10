using Microsoft.EntityFrameworkCore;
using Sample.API.Entities;
using Sample.API.ReadModels;

namespace Sample.API.Data;

public class ReadDbContext(DbContextOptions<ReadDbContext> opts) : DbContext(opts)
{
    public DbSet<CustomerRead> Customers => Set<CustomerRead>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerRead>().HasKey(c => c.Id);
        modelBuilder.Entity<ProcessedEvent>().HasIndex(p => p.EventId).IsUnique();
    }
}