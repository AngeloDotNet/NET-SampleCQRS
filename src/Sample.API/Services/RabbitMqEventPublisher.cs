using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using Sample.API.Services.Interfaces;
using Sample.API.Settings;

namespace Sample.API.Services;

public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IRabbitMqConnection connection;
    private readonly RabbitConfig config;
    private readonly PublisherConfig pubCfg;
    private readonly AsyncRetryPolicy retryPolicy;
    private readonly ILogger<RabbitMqEventPublisher> logger;
    private readonly Random jitter = new();

    public RabbitMqEventPublisher(IRabbitMqConnection connection, IOptions<RabbitConfig> cfg, IOptions<PublisherConfig> pubCfg,
        ILogger<RabbitMqEventPublisher> logger)
    {
        this.connection = connection;
        config = cfg.Value;
        this.pubCfg = pubCfg.Value;
        this.logger = logger;

        retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(this.pubCfg.RetryCount, (int attempt) =>
            {
                // exponential backoff with jitter
                var backoff = Math.Pow(2, attempt) * this.pubCfg.BaseDelayMs;
                var jitter = this.jitter.Next(0, this.pubCfg.MaxJitterMs);
                return TimeSpan.FromMilliseconds(backoff + jitter);
            }, onRetry: (Exception ex, TimeSpan ts, int attempt, Context ctx) =>
            {
                this.logger.LogWarning(ex, "Publish attempt {Attempt} failed, next retry in {Delay}", attempt, ts);
            });
    }

    public async Task PublishRawAsync(string type, Guid messageId, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        // Execute publish with retry policy
        await retryPolicy.ExecuteAsync(async token =>
        {
            // create a new channel per publish - short-lived (acceptable for many scenarios).
            using var channel = connection.GetConnection().CreateModel();

            // declare exchange
            channel.ExchangeDeclare(exchange: config.Exchange, type: config.ExchangeType, durable: true, autoDelete: false);

            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = messageId.ToString();
            props.Type = type;

            // BasicPublish is sync so wrap it in Task.Run to avoid blocking if desired
            channel.BasicPublish(exchange: config.Exchange, routingKey: string.Empty, basicProperties: props, body: body.ToArray());

            // small await to satisfy async delegate - no-op
            await Task.CompletedTask;
        }, cancellationToken);
    }
}