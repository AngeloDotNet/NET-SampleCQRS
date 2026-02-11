using RabbitMQ.Client;

namespace Sample.API.Services.Interfaces;

public interface IRabbitMqConnection : IDisposable
{
    IConnection GetConnection();
}