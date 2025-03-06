namespace SeaScope.Services
{
    public interface IKafkaConsumerService
    {
        Task StartAsync(CancellationToken cancellationToken);
        void Pause();
        void Resume();
    }
}
