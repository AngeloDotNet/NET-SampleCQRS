namespace Sample.API.Settings;

public class RabbitConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "customer.events";
    public string ExchangeType { get; set; } = "fanout";
    public string Queue { get; set; } = "customer.events.queue";
    public string DlqExchange { get; set; } = "customer.events.dlx";
    public string DlqQueue { get; set; } = "customer.events.dlq";
}