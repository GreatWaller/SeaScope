using System.Collections.Concurrent;

namespace SeaScope.Services
{
    public interface IKafkaConsumerService
    {
        Task StartAsync(CancellationToken cancellationToken);
        void Pause();
        void Resume();
        ConcurrentDictionary<string, (double Lat, double Lon)> ActiveCameras { get; }
    }
}
