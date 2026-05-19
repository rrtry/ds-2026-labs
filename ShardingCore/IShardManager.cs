using StackExchange.Redis;

namespace ShardingCore;

public interface IShardManager
{
    Task<string?> GetShardKeyAsync(string id);
    Task SaveShardKeyAsync(string id, string region);
    IDatabase GetMainDatabase();
    IDatabase GetShardDatabase(string region);
    IServer GetShardServer(string region);
    (string Region, IDatabase Db) GetShardById(string id);
}