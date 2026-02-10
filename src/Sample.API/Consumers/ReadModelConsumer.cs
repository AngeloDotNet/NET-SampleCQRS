using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sample.API.Data;
using Sample.API.Entities;
using Sample.API.Events;
using Sample.API.ReadModels;
using Sample.API.Services.Interfaces;
using Sample.API.Settings;

namespace Sample.API.Consumers;

public class ReadModelConsumer(IRabbitMqConnection connection, IOptions<RabbitConfig> cfg, IServiceScopeFactory scopeFactory, ILogger<ReadModelConsumer> logger) : BackgroundService
{
    private readonly RabbitConfig cfg = cfg.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = connection.GetConnection().CreateModel();

        // declare main exchange
        channel.ExchangeDeclare(cfg.Exchange, cfg.ExchangeType, durable: true);
        // declare DLX exchange and DLQ
        channel.ExchangeDeclare(cfg.DlqExchange, ExchangeType.Fanout, durable: true);

        channel.QueueDeclare(cfg.DlqQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.QueueBind(cfg.DlqQueue, cfg.DlqExchange, "");

        // queue arguments: set dead-letter-exchange
        var queueArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = cfg.DlqExchange
        };

        channel.QueueDeclare(cfg.Queue, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
        channel.QueueBind(cfg.Queue, cfg.Exchange, "");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();

            try
            {
                var evt = JsonSerializer.Deserialize<CustomerCreatedEvent>(body) ?? throw new Exception("Invalid event payload");

                using var scope = scopeFactory.CreateScope();
                var readDb = scope.ServiceProvider.GetRequiredService<ReadDbContext>();

                // Idempotency: if event processed, ack and return
                var already = await readDb.ProcessedEvents.AnyAsync(p => p.EventId == evt.EventId);

                if (already)
                {
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                var data = JsonSerializer.Deserialize<JsonElement>(evt.Data);
                var id = data.GetProperty("Id").GetGuid();

                var name = data.GetProperty("Name").GetString()!;
                var email = data.GetProperty("Email").GetString()!;

                var existing = await readDb.Customers.FindAsync(id);
                if (existing == null)
                {
                    readDb.Customers.Add(new CustomerRead
                    {
                        Id = id,
                        Name = name,
                        Email = email,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Name = name;
                    existing.Email = email;
                    existing.UpdatedAt = DateTime.UtcNow;
                }

                readDb.ProcessedEvents.Add(new ProcessedEvent { EventId = evt.EventId, ProcessedAt = DateTime.UtcNow });

                await readDb.SaveChangesAsync();

                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing message, rejecting to DLQ");
                channel.BasicReject(ea.DeliveryTag, requeue: false);
            }
        };

        channel.BasicConsume(queue: cfg.Queue, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}