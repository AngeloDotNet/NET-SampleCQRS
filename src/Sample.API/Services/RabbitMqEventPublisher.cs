using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Sample.API.Services.Interfaces;
using Sample.API.Settings;

namespace Sample.API.Services;

public class RabbitMqEventPublisher(IRabbitMqConnection connection, IOptions<RabbitConfig> cfg) : IEventPublisher
{
    private readonly RabbitConfig config = cfg.Value;

    public Task PublishRawAsync(string type, Guid messageId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        using var channel = connection.GetConnection().CreateModel();

        // declare exchange
        channel.ExchangeDeclare(exchange: config.Exchange, type: config.ExchangeType, durable: true, autoDelete: false);

        var props = channel.CreateBasicProperties();

        props.Persistent = true;
        props.MessageId = messageId.ToString();
        props.Type = type;

        channel.BasicPublish(exchange: config.Exchange, routingKey: string.Empty, basicProperties: props, body: body.ToArray());

        return Task.CompletedTask;
    }
}