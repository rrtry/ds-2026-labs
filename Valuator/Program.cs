using RabbitMQ.Client;
using Valuator.Services;
using ShardingCore;

namespace Valuator;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IShardManager, ShardManager>();

        // Регистрация RabbitMQ connection
        builder.Services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        builder.Services.AddScoped<IRankMessageProducer, RabbitMqRankProducer>();
        builder.Services.AddScoped<IEventProducer, RabbitMqEventProducer>();

        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();

        await app.RunAsync();
    }
}
