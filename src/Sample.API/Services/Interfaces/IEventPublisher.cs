namespace Sample.API.Services.Interfaces;

public interface IEventPublisher
{
    Task PublishRawAsync(string type, Guid messageId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default);
}