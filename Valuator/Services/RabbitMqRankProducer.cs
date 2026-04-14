using System.Text;
using RabbitMQ.Client;

namespace Valuator.Services;

public interface IRankMessageProducer
{
    Task PublishAsync(string id);
}

public class RabbitMqRankProducer : IRankMessageProducer, IAsyncDisposable
{
    private const string QueueName = "valuator.processing.rank";
    private readonly IConnection _connection;
    private IChannel? _channel;

    public RabbitMqRankProducer(IConnection connection)
    {
        _connection = connection;
    }

    private async Task EnsureChannelAsync()
    {
        if (_channel == null || _channel.IsClosed)
        {
            _channel = await _connection.CreateChannelAsync();
            await DeclareTopologyAsync();
        }
    }

    private async Task DeclareTopologyAsync()
    {
        await _channel!.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);
    }

    public async Task PublishAsync(string id)
    {
        await EnsureChannelAsync();
        byte[] body = Encoding.UTF8.GetBytes(id);
        await _channel!.BasicPublishAsync(
            exchange: "",
            routingKey: QueueName,
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.CloseAsync();
    }
}