namespace Sample.API.Settings;

public class PublisherConfig
{
    public int RetryCount { get; set; } = 5;
    public int BaseDelayMs { get; set; } = 200; // base delay for exponential backoff
    public int MaxJitterMs { get; set; } = 100; // jitter max
}