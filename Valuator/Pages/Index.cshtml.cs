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

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redisConnection, IRankMessageProducer rankProducer)
    {
        _logger = logger;
        _redisConnection = redisConnection;
        _rankProducer = rankProducer;
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