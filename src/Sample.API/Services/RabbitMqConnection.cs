using RabbitMQ.Client;
using Sample.API.Services.Interfaces;

namespace Sample.API.Services;

public class RabbitMqConnection(ConnectionFactory factory) : IRabbitMqConnection
{
    private IConnection? connection;

    public IConnection GetConnection()
    {
        if (connection == null || !connection.IsOpen)
        {
            connection = factory.CreateConnection();
        }

        return connection;
    }

    public void Dispose()
    {
        try
        {
            connection?.Dispose();
        }
        catch { }
    }
}