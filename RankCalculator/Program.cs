using System.Text;
using System.Globalization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

const string QueueName = "valuator.processing.rank";

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

var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

Console.WriteLine("RankCalculator started. Waiting for messages...");

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (_, ea) =>
{
    var id = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"[RankCalculator] Processing id: {id}");

    try
    {
        var text = await db.StringGetAsync($"TEXT-{id}");
        if (text.IsNullOrEmpty)
        {
            Console.WriteLine($"Text for {id} not found, rejecting (no requeue)");
            await channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return;
        }

        double rank = CalculateRank(text.ToString());
        await db.StringSetAsync($"RANK-{id}", rank.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine($"[RankCalculator] Rank for {id} = {rank}");

        await channel.BasicAckAsync(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing {id}: {ex.Message}");
        await channel.BasicNackAsync(ea.DeliveryTag, false, true); // requeue
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