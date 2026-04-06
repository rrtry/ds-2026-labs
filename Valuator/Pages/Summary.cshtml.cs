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

    public IActionResult OnGet(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("Index");
        }

        Id = id;
        _logger.LogDebug($"Loading data for ID: {id}");

        var db = _redisConnection.GetDatabase();

        // Получаем текст из Redis
        var textValue = db.StringGet($"TEXT-{id}");
        Text = textValue.IsNullOrEmpty ? "Not found" : textValue.ToString();

        // Получаем rank из Redis
        var rankValue = db.StringGet($"RANK-{id}");
        if (!rankValue.IsNullOrEmpty)
        {
            if (double.TryParse(rankValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rank))
            {
                Rank = rank;
            }
        }

        // Получаем similarity из Redis
        var similarityValue = db.StringGet($"SIMILARITY-{id}");
        if (!similarityValue.IsNullOrEmpty)
        {
            if (double.TryParse(similarityValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double similarity))
            {
                Similarity = similarity;
            }
        }

        Console.WriteLine($"Retrieved data for ID: {id}, Text: {Text}, Rank: {Rank}, Similarity: {Similarity}");
        return Page();
    }
}
