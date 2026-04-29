using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

const string ExchangeName = "events";

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<Program>();

var connectionFactory = new ConnectionFactory { HostName = "localhost" };
using var connection = await connectionFactory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Объявляем exchange
await channel.ExchangeDeclareAsync(
    exchange: ExchangeName,
    type: ExchangeType.Topic,
    durable: true
);

// Объявляем очередь
var queueDeclareResult = await channel.QueueDeclareAsync(
    queue: "",
    durable: true,
    exclusive: true,
    autoDelete: true
);

string queueName = queueDeclareResult.QueueName;
logger.LogInformation(queueName, ExchangeName, $"Created unique queue: {queueName}");

// Привязываем к routing key для обоих событий
await channel.QueueBindAsync(queueName, ExchangeName, "rank.calculated");
await channel.QueueBindAsync(queueName, ExchangeName, "similarity.calculated");

logger.LogInformation("EventsLogger started. Waiting for events...");
// Console.WriteLine("EventsLogger started. Waiting for events...");

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
            logger.LogInformation($"[EVENT] Type: RankCalculated, Id: {id}, Rank: {rank}");
            //Console.WriteLine($"[EVENT] Type: RankCalculated, Id: {id}, Rank: {rank}");
        }
        else if (routingKey == "similarity.calculated")
        {
            var similarity = jsonDoc.RootElement.GetProperty("Similarity").GetDouble();
            logger.LogInformation($"[EVENT] Type: SimilarityCalculated, Id: {id}, Similarity: {similarity}");
            //Console.WriteLine($"[EVENT] Type: SimilarityCalculated, Id: {id}, Similarity: {similarity}");
        }

        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        logger.LogError($"Error processing event: {ex.Message}");
        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
    }
};

await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer);
await Task.Delay(-1);