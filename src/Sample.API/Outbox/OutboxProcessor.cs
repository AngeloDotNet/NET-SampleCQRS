using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sample.API.Data;
using Sample.API.Services.Interfaces;
using Sample.API.Settings;

namespace Sample.API.Outbox;

public class OutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<OutboxConfig> cfg, IEventPublisher publisher, ILogger<OutboxProcessor> logger) : BackgroundService
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
                        // Claim: increment Attempts and persist so that RowVersion changes.
                        msg.Attempts++;
                        await writeDb.SaveChangesAsync(stoppingToken);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Another worker updated this row first (race). Skip it.
                        logger.LogDebug("Outbox message {MessageId} was claimed/updated by another worker. Skipping.", msg.MessageId);
                        // Reload entry from DB and continue (we won't process this copy)
                        continue;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to claim outbox message {MessageId}", msg.MessageId);
                        continue;
                    }

                    try
                    {
                        // publish raw (we stored serialized event object as Data)
                        var bytes = System.Text.Encoding.UTF8.GetBytes(msg.Data);
                        await publisher.PublishRawAsync(msg.Type, msg.MessageId, bytes, stoppingToken);

                        // mark dispatched
                        msg.DispatchedAt = DateTime.UtcNow;
                        msg.LastError = null;

                        try
                        {
                            await writeDb.SaveChangesAsync(stoppingToken);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            // Another worker may have updated/dispatched it after our claim => safe to ignore
                            logger.LogInformation("Outbox message {MessageId} was concurrently modified when setting DispatchedAt. Likely dispatched by another instance.", msg.MessageId);
                        }
                    }
                    catch (Exception ex)
                    {
                        // publishing failed (Polly already retried according to policy)
                        msg.LastError = ex.Message;
                        logger.LogError(ex, "Failed to publish outbox message {MessageId} after retries", msg.MessageId);
                        // persist last error and continue
                        try
                        {
                            await writeDb.SaveChangesAsync(stoppingToken);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            logger.LogInformation("Concurrent update while saving error info for message {MessageId}", msg.MessageId);
                        }
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