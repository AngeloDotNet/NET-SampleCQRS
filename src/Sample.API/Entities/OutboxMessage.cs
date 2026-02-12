using System.ComponentModel.DataAnnotations;

namespace Sample.API.Entities;

public class OutboxMessage
{
    public int Id { get; set; }
    public Guid MessageId { get; set; } = Guid.NewGuid(); // unique idempotency key
    public string Type { get; set; } = default!;
    public string Data { get; set; } = default!; // JSON payload
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // dispatch metadata
    public DateTime? DispatchedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    // ROWVERSION for optimistic concurrency (SQL Server)
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}