using StackExchange.Redis;
using RabbitMQ.Client;
using Valuator.Services;
using Valuator.Hubs;

namespace Valuator;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect("localhost:6379")
        );

        // Регистрация RabbitMQ connection
        builder.Services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        builder.Services.AddScoped<IRankMessageProducer, RabbitMqRankProducer>();
        builder.Services.AddScoped<IEventProducer, RabbitMqEventProducer>();

        builder.Services.AddSignalR();
        builder.Services.AddHostedService<RankEventConsumer>();

        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseWebSockets();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapHub<RankHub>("/rankHub");
        app.MapRazorPages();

        await app.RunAsync();
    }
}
