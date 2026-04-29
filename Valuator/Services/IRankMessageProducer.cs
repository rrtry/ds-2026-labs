public interface IRankMessageProducer
{
    Task PublishRankAsync(string id);
}