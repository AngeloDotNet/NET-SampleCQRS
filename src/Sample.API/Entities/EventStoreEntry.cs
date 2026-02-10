namespace Sample.API.Entities;

public class EventStoreEntry
{
    public int Id { get; set; }
    public Guid EventId { get; set; }
    public Guid AggregateId { get; set; }
    public string Type { get; set; } = default!;
    public string Data { get; set; } = default!;
    public DateTime OccurredAt { get; set; }
}