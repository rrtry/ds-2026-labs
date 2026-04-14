using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Valuator.Services;

public class RabbitMqEventProducer : IEventProducer, IAsyncDisposable
{
    private const string ExchangeName = "events";
    private readonly IConnection _connection;
    private IChannel? _channel;

    public RabbitMqEventProducer(IConnection connection)
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
        // Объявляем topic exchange
        await _channel!.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true
        );
    }

    public async Task PublishSimilarityCalculatedAsync(string id, double similarity)
    {
        await EnsureChannelAsync();
        var message = new { Id = id, Similarity = similarity };
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await _channel!.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "similarity.calculated",
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.CloseAsync();
    }
}