using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sample.API.Data;
using Sample.API.Services.Interfaces;
using Sample.API.Settings;

namespace Sample.API.Outbox;

public class OutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<OutboxConfig> cfg, IEventPublisher publisher, ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    private readonly OutboxConfig cfg = cfg.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxProcessor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var writeDb = scope.ServiceProvider.GetRequiredService<WriteDbContext>();

                // Pick batch of not dispatched
                var batch = await writeDb.OutboxMessages
                    .Where(o => o.DispatchedAt == null)
                    .OrderBy(o => o.OccurredAt)
                    .Take(cfg.BatchSize)
                    .ToListAsync(stoppingToken);

                foreach (var msg in batch)
                {
                    try
                    {
                        // optimistic: increment Attempts and mark dispatched time before sending to reduce double send window
                        msg.Attempts++;
                        await writeDb.SaveChangesAsync(stoppingToken); // persist attempt increment

                        // publish raw (we stored serialized event object as Data)
                        var bytes = System.Text.Encoding.UTF8.GetBytes(msg.Data);
                        await publisher.PublishRawAsync(msg.Type, msg.MessageId, bytes, stoppingToken);

                        msg.DispatchedAt = DateTime.UtcNow;
                        msg.LastError = null;
                        await writeDb.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        msg.LastError = ex.Message;
                        logger.LogError(ex, "Failed to publish outbox message {MessageId}", msg.MessageId);
                        await writeDb.SaveChangesAsync(stoppingToken);
                        // do not throw — continue with others
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxProcessor loop failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(cfg.PollingIntervalSeconds), stoppingToken);
        }
    }
}