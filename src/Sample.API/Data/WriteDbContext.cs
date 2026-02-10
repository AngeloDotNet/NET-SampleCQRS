using Microsoft.EntityFrameworkCore;
using Sample.API.Entities;

namespace Sample.API.Data;

public class WriteDbContext(DbContextOptions<WriteDbContext> opts) : DbContext(opts)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<EventStoreEntry> EventStore => Set<EventStoreEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasKey(c => c.Id);
        modelBuilder.Entity<EventStoreEntry>().HasIndex(e => e.EventId).IsUnique();

        modelBuilder.Entity<OutboxMessage>().HasIndex(o => o.MessageId).IsUnique();
    }
}