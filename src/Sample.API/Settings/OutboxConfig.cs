namespace Sample.API.Settings;

public class OutboxConfig
{
    public int PollingIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
}