namespace Sample.API.Entities;

public class ProcessedEvent
{
    public int Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime ProcessedAt { get; set; }
}