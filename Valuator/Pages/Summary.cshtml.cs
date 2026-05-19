using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using System.Globalization;

using ShardingCore;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IShardManager _shardManager;

    public SummaryModel(ILogger<SummaryModel> logger, IShardManager shardManager)
    {
        _logger = logger;
        _shardManager = shardManager;
    }

    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Rank { get; set; } = 0.0;
    public double Similarity { get; set; } = 0.0;
    public bool IsRankCalculated { get; set; } = false;

    public async Task<IActionResult> OnGet(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("Index");
        }

        Id = id;
        _logger.LogDebug($"Loading data for ID: {id}");

        var region = await _shardManager.GetShardKeyAsync(id);
        if (region == null)
        {
            return NotFound("Region ID not found");
        }

        var db = _shardManager.GetShardDatabase(region);

        var textValue = db.StringGet($"TEXT-{id}");
        Text = textValue.IsNullOrEmpty ? "Not found" : textValue.ToString();

        // Проверяем, существует ли ключ ранга
        var rankKey = $"RANK-{id}";
        IsRankCalculated = db.KeyExists(rankKey);
        if (IsRankCalculated)
        {
            var rankValue = db.StringGet(rankKey);
            if (!rankValue.IsNullOrEmpty && 
                double.TryParse(rankValue.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rank))
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
