using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Valuator.Hubs;

namespace Valuator.Services;

public class RankEventConsumer : BackgroundService
{
    private readonly IHubContext<RankHub> _hubContext;
    private IConnection _connection;
    private IChannel _channel;
    private string _queueName;
    private const string ExchangeName = "events";
    private const string RoutingKey = "rank.calculated";

    public RankEventConsumer(IHubContext<RankHub> hubContext)
    {
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken
        );

        var queueDeclareResult = await _channel.QueueDeclareAsync(
            queue: "",
            durable: true,
            exclusive: true,
            autoDelete: true,
            cancellationToken: stoppingToken
        );
        _queueName = queueDeclareResult.QueueName;

        await _channel.QueueBindAsync(
            queue: _queueName,
            exchange: ExchangeName,
            routingKey: RoutingKey,
            cancellationToken: stoppingToken
        );

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceived;

        await _channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Нормальная остановка
        }
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        try
        {
            using var doc = JsonDocument.Parse(message);
            var id = doc.RootElement.GetProperty("Id").GetString();
            var rank = doc.RootElement.GetProperty("Rank").GetDouble();
            
            await _hubContext.Clients.Group(id).SendAsync("RankReady", rank);
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing rank event: {ex.Message}");
            await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
        }
    }

    public override async void Dispose()
    {
        if (_channel != null)
            await _channel.CloseAsync();
        if (_connection != null)
            await _connection.CloseAsync();
        base.Dispose();
    }
}