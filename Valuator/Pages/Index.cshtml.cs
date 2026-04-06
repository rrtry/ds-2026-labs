using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConnectionMultiplexer _redisConnection;

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redisConnection)
    {
        _logger = logger;
        _redisConnection = redisConnection;
    }

    public void OnGet()
    {

    }

    public IActionResult OnPost(string text)
    {
        _logger.LogDebug(text);

        if (string.IsNullOrEmpty(text))
        {
            return Redirect("/index");
        }

        string id = Guid.NewGuid().ToString();
        var db = _redisConnection.GetDatabase();
        
        double similarity = CalculateSimilarity(text, db);
        
        string textKey = "TEXT-" + id;
        db.StringSet(textKey, text);

        double rank = CalculateRank(text);
        string rankKey = "RANK-" + id;
        db.StringSet(rankKey, rank.ToString(CultureInfo.InvariantCulture));

        string similarityKey = "SIMILARITY-" + id;
        db.StringSet(similarityKey, similarity.ToString(CultureInfo.InvariantCulture));

        return Redirect($"summary?id={id}");
    }

    private double CalculateRank(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0.0;
        }
        
        // Подсчет неалфавитных символов
        int nonAlphabeticCount = 0;
        int totalLength = text.Length;
        
        foreach (char c in text)
        {
            if (!IsAlphabetic(c))
            {
                nonAlphabeticCount++;
            }
        }
        
        return totalLength > 0 ? (double)nonAlphabeticCount / totalLength : 0.0;
    }

    private bool IsAlphabetic(char c)
    {
        // Латинские буквы (строчные и прописные)
        if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
            return true;
        
        // Русские буквы (строчные и прописные)
        if ((c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я'))
            return true;
        
        // Отдельно проверяем букву 'ё', так как она вне диапазона
        if (c == 'ё' || c == 'Ё')
            return true;
        
        return false;
    }

    private double CalculateSimilarity(string text, IDatabase db)
    {
        if (string.IsNullOrEmpty(text))
            return 0.0;

        // Получаем все ключи TEXT-* из Redis
        var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());
        var textKeys = server.Keys(pattern: "TEXT-*").ToList();
        
        foreach (var key in textKeys)
        {
            var existingText = db.StringGet(key);
            if (!existingText.IsNullOrEmpty && existingText.ToString() == text)
            {
                // Найден дубликат
                return 1.0;
            }
        }

        // Дубликат не найден
        return 0.0;
    }
}
