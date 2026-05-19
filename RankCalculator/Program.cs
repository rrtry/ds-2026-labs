using System.Text;
using System.Text.Json;
using System.Globalization;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Microsoft.Extensions.Logging;
using ShardingCore;

const string QueueName = "valuator.processing.rank";
const string ExchangeName = "events";

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ShardManager>();

// Настройка RabbitMQ
var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(
    queue: QueueName,
    durable: true,
    exclusive: false,
    autoDelete: false
);

await channel.ExchangeDeclareAsync(
    exchange: ExchangeName,
    type: ExchangeType.Topic,
    durable: true
);

var shardManager = new ShardManager(logger);
Console.WriteLine("RankCalculator started. Waiting for messages...");

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (_, ea) =>
{
    var id = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"[RankCalculator] Processing id: {id}");

    try
    {
        // Регион
        var region = await shardManager.GetShardKeyAsync(id);
        if (region == null)
        {
            Console.WriteLine($"No shard key for {id}, rejecting");
            await channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return;
        }

        // Сегмент
        var shardDb = shardManager.GetShardDatabase(region);
        var text = await shardDb.StringGetAsync($"TEXT-{id}");

        if (text.IsNullOrEmpty)
        {
            Console.WriteLine($"Text for {id} not found in shard {region}, rejecting");
            await channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return;
        }

        double rank = CalculateRank(text.ToString());
        await shardDb.StringSetAsync($"RANK-{id}", rank.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine($"[RankCalculator] Rank for {id} = {rank}");

        var eventMessage = new { Id = id, Rank = rank };
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventMessage));

        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "rank.calculated",
            body: body
        );

        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing {id}: {ex.Message}");
        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
    }
};

await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer);
await Task.Delay(-1);

static double CalculateRank(string text)
{
    if (string.IsNullOrEmpty(text)) return 0.0;
    int nonAlphabetic = text.Count(c => !IsAlphabetic(c));
    return (double)nonAlphabetic / text.Length;
}

static bool IsAlphabetic(char c) => char.IsLetter(c) || c == 'ё' || c == 'Ё';