namespace SeaScope.Services
{
    public interface IProjectionService
    {
        void AddShip(string camId, string mmsi, double lat, double lon);
        Task StartAsync(CancellationToken cancellationToken);
    }
}
