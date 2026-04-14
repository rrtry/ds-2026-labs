using System.Text;
using System.Text.Json;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

const string ExchangeName = "events";
const string QueueName = "events.logger"; // фиксированная очередь для логгера

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Объявляем exchange
await channel.ExchangeDeclareAsync(
    exchange: ExchangeName,
    type: ExchangeType.Topic,
    durable: true
);

// Объявляем очередь
await channel.QueueDeclareAsync(
    queue: QueueName,
    durable: true,
    exclusive: false,
    autoDelete: false
);

// Привязываем к routing key для обоих событий
await channel.QueueBindAsync(QueueName, ExchangeName, "rank.calculated");
await channel.QueueBindAsync(QueueName, ExchangeName, "similarity.calculated");

Console.WriteLine("EventsLogger started. Waiting for events...");

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (_, ea) =>
{
    var routingKey = ea.RoutingKey;
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    
    try
    {
        var jsonDoc = JsonDocument.Parse(message);
        var id = jsonDoc.RootElement.GetProperty("Id").GetString();

        if (routingKey == "rank.calculated")
        {
            var rank = jsonDoc.RootElement.GetProperty("Rank").GetDouble();
            Console.WriteLine($"[EVENT] Type: RankCalculated, Id: {id}, Rank: {rank}");
        }
        else if (routingKey == "similarity.calculated")
        {
            var similarity = jsonDoc.RootElement.GetProperty("Similarity").GetDouble();
            Console.WriteLine($"[EVENT] Type: SimilarityCalculated, Id: {id}, Similarity: {similarity}");
        }

        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing event: {ex.Message}");
        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
    }
};

await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer);
await Task.Delay(-1);