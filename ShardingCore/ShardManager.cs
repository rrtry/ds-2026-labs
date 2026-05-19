using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace ShardingCore;

public class ShardManager : IShardManager
{
    private readonly IConnectionMultiplexer _mainRedis;
    private readonly Dictionary<string, IConnectionMultiplexer> _shardRedis = new();
    private readonly ILogger<ShardManager> _logger;

    public ShardManager(ILogger<ShardManager> logger)
    {
        _logger = logger;

        var mainConn = Environment.GetEnvironmentVariable("DB_MAIN") ?? "localhost:6379";
        _mainRedis = ConnectionMultiplexer.Connect(mainConn);

        var regions = new[] { "RU", "EU", "ASIA" };
        foreach (var region in regions)
        {
            var envVar = $"DB_{region}";
            var connStr = Environment.GetEnvironmentVariable(envVar);
            
            if (string.IsNullOrEmpty(connStr))
            {
                throw new Exception($"Environment variable {envVar} not set");
            }

            _shardRedis[region] = ConnectionMultiplexer.Connect(connStr);
        }
    }

    public IDatabase GetMainDatabase() => _mainRedis.GetDatabase();

    public IDatabase GetShardDatabase(string region) =>
        _shardRedis.TryGetValue(region, out var mux) ? mux.GetDatabase() : throw new Exception($"Unknown region {region}");

    public IServer GetShardServer(string region) =>
        _shardRedis.TryGetValue(region, out var mux) ? mux.GetServer(mux.GetEndPoints().First()) : throw new Exception($"Unknown region {region}");

    public async Task<string?> GetShardKeyAsync(string id)
    {
        var db = GetMainDatabase();
        var region = await db.StringGetAsync($"SHARD-{id}");

        if (region.IsNullOrEmpty)
        {
            return null;
        }

        var regionStr = region.ToString();
        _logger.LogInformation("LOOKUP: {Id}, {Region}", id, regionStr);
        return regionStr;
    }

    public async Task SaveShardKeyAsync(string id, string region)
    {
        var db = GetMainDatabase();
        await db.StringSetAsync($"SHARD-{id}", region);
        _logger.LogInformation("SAVE SHARD: {Id} -> {Region}", id, region);
    }

    public (string Region, IDatabase Db) GetShardById(string id)
    {
        throw new NotImplementedException();
        /*
        var regionTask = GetShardKeyAsync(id);
        regionTask.Wait();
        var region = regionTask.Result;

        if (region == null)
        {
            throw new Exception($"No shard key for id {id}");
        }

        return (region, GetShardDatabase(region)); */
    }
}