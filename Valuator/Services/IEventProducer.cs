namespace Valuator.Services;

public interface IEventProducer
{
    Task PublishSimilarityCalculatedAsync(string id, double similarity);
}