using System.Globalization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

using Valuator.Services;
using ShardingCore;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IShardManager _shardManager;
    private readonly IRankMessageProducer _rankProducer;
    private readonly IEventProducer _eventProducer;

    public List<(string Country, string Region)> Countries => new()
    {
        ("Russia",  "RU"),
        ("France",  "EU"),
        ("Germany", "EU"),
        ("UAE",     "ASIA"),
        ("India",   "ASIA")
    };

    [BindProperty]
    public string SelectedCountry { get; set; } = string.Empty;

    public IndexModel(
        ILogger<IndexModel> logger, 
        IShardManager shardManager, 
        IRankMessageProducer rankProducer,
        IEventProducer eventProducer)
    {
        _logger = logger;
        _shardManager  = shardManager;
        _rankProducer  = rankProducer;
        _eventProducer = eventProducer;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPost(string text)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(SelectedCountry))
        {
            return Redirect("/index");
        }

        // Определяем регион по выбранной стране
        string region = Countries.First(c => c.Country == SelectedCountry).Region;

        string id = Guid.NewGuid().ToString();

        // 1. Сохраняем в Main: ID -> регион
        await _shardManager.SaveShardKeyAsync(id, region);

        // 2. Получаем БД сегмента
        var shardDb = _shardManager.GetShardDatabase(region);

        // 3. Вычисляем similarity в пределах этого сегмента
        double similarity = await CalculateSimilarityInShard(text, shardDb, region);
        await shardDb.StringSetAsync($"SIMILARITY-{id}", similarity.ToString(CultureInfo.InvariantCulture));

        // 4. Уведомляем о similarity (через RabbitMQ)
        await _eventProducer.PublishSimilarityCalculatedAsync(id, similarity);

        // 5. Сохраняем текст в сегменте
        await shardDb.StringSetAsync($"TEXT-{id}", text);

        // 6. Отправляем задание на вычисление ранга
        await _rankProducer.PublishRankAsync(id);

        return Redirect($"summary?id={id}");
    }

    private async Task<double> CalculateSimilarityInShard(string text, IDatabase db, string region)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0.0;
        }

        var server = _shardManager.GetShardServer(region);
        var keys = server.Keys(pattern: "TEXT-*").ToList();

        foreach (var key in keys)
        {
            var existingText = await db.StringGetAsync(key);
            if (!existingText.IsNullOrEmpty && existingText.ToString() == text)
            {
                return 1.0;
            }
        }

        return 0.0;
    }
}
