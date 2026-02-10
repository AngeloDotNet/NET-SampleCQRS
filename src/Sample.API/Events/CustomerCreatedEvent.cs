namespace Sample.API.Events;

public class CustomerCreatedEvent
{
    public Guid EventId { get; set; }
    public Guid AggregateId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Data { get; set; } = default!; // json payload
}