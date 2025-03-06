using Microsoft.AspNetCore.SignalR;
using SeaScope.Hubs;
using System.Collections.Concurrent;

namespace SeaScope.Services
{
    public class ProjectionService : IProjectionService
    {
        private readonly IHubContext<CameraHub> _hubContext;
        private readonly ConcurrentDictionary<string, Projector> _projectors = new();

        public ProjectionService(IHubContext<CameraHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void AddShip(string camId, string mmsi, double lat, double lon)
        {
            var projector = _projectors.GetOrAdd(camId, key => new Projector(camId, _hubContext));
            projector.AddShip(mmsi, lat, lon);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Projector线程在AddShip时动态启动，无需额外启动逻辑
            await Task.CompletedTask;
        }
    }
}
