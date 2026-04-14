using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using Valuator.Services;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly IRankMessageProducer _rankProducer;
    private readonly IEventProducer _eventProducer;


    public IndexModel(ILogger<IndexModel> logger, 
    IConnectionMultiplexer redisConnection, 
    IRankMessageProducer rankProducer,
    IEventProducer eventProducer)
    {
        _logger = logger;
        _redisConnection = redisConnection;
        _rankProducer = rankProducer;
        _eventProducer = eventProducer;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPost(string text)
    {
        _logger.LogDebug(text);

        if (string.IsNullOrEmpty(text))
        {
            return Redirect("/index");
        }

        string id = Guid.NewGuid().ToString();
        var db = _redisConnection.GetDatabase();

        // Синхронно вычисляем similarity
        double similarity = CalculateSimilarity(text, db);
        db.StringSet($"SIMILARITY-{id}", similarity.ToString(CultureInfo.InvariantCulture));

        // Уведомляем о вычисленном similarity
        await _eventProducer.PublishSimilarityCalculatedAsync(id, similarity);
        
        // Сохраняем текст
        db.StringSet($"TEXT-{id}", text);

        // Отправляем задание на вычисление ранга в очередь
        await _rankProducer.PublishAsync(id);

        return Redirect($"summary?id={id}");
    }

    private double CalculateSimilarity(string text, IDatabase db)
    {
        if (string.IsNullOrEmpty(text))
            return 0.0;

        var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());
        var textKeys = server.Keys(pattern: "TEXT-*").ToList();

        foreach (var key in textKeys)
        {
            var existingText = db.StringGet(key);
            if (!existingText.IsNullOrEmpty && existingText.ToString() == text)
                return 1.0;
        }

        return 0.0;
    }
}
