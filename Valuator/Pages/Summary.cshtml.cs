using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.Globalization;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IConnectionMultiplexer _redisConnection;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redisConnection)
    {
        _logger = logger;
        _redisConnection = redisConnection;
    }

    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Rank { get; set; } = 0.0;
    public double Similarity { get; set; } = 0.0;
    public bool IsRankCalculated { get; set; } = false;

    public IActionResult OnGet(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("Index");
        }

        Id = id;
        _logger.LogDebug($"Loading data for ID: {id}");

        var db = _redisConnection.GetDatabase();

        var textValue = db.StringGet($"TEXT-{id}");
        Text = textValue.IsNullOrEmpty ? "Not found" : textValue.ToString();

        // Проверяем, существует ли ключ ранга
        var rankKey = $"RANK-{id}";
        IsRankCalculated = db.KeyExists(rankKey);
        if (IsRankCalculated)
        {
            var rankValue = db.StringGet(rankKey);
            if (!rankValue.IsNullOrEmpty && double.TryParse(rankValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rank))
            {
                Rank = rank;
            }
        }

        var similarityValue = db.StringGet($"SIMILARITY-{id}");
        if (!similarityValue.IsNullOrEmpty && double.TryParse(similarityValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double similarity))
        {
            Similarity = similarity;
        }

        Console.WriteLine($"Retrieved data for ID: {id}, Text: {Text}, Rank: {Rank}, Similarity: {Similarity}");
        return Page();
    }
}