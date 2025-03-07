using Microsoft.AspNetCore.SignalR;
using SeaScope.Hubs;
using System.Collections.Concurrent;

namespace SeaScope.Services
{
    public class Projector
    {
        private readonly string _camId;
        private readonly IHubContext<CameraHub> _hubContext;
        private readonly ConcurrentQueue<(string Mmsi, double Lat, double Lon)> _shipQueue = new();
        private Task _processingTask;

        public Projector(string camId, IHubContext<CameraHub> hubContext)
        {
            _camId = camId;
            _hubContext = hubContext;
            _processingTask = Task.Run(() => ProcessQueueAsync());
        }

        public void AddShip(string mmsi, double lat, double lon)
        {
            _shipQueue.Enqueue((mmsi, lat, lon));
        }

        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                if (_shipQueue.TryDequeue(out var ship))
                {
                    await ProjectToCamera(ship);
                }
                await Task.Delay(100); // 避免忙等待
            }
        }

        private async Task ProjectToCamera((string Mmsi, double Lat, double Lon) ship)
        {
            // 检查当前相机组是否有客户端
            if (!CameraHub.HasClientsInGroup(_camId))
            {
                return; // 没有客户端连接，跳过投影计算
            }
            var (screenX, screenY) = ConvertToScreenCoords(ship.Lat, ship.Lon);
            var projectionData = new { CameraId = _camId, Mmsi = ship.Mmsi, X = screenX, Y = screenY };
            Console.WriteLine(projectionData);
            if (screenX <= 0 && screenY <= 0) // 超出范围
            {
                return;
            }
            await _hubContext.Clients.Group(_camId)
                .SendAsync("ReceiveProjection", projectionData);
        }

        private (int X, int Y) ConvertToScreenCoords(double lat, double lon)
        {
            // 投影逻辑待实现
            var point = CameraController.ConvertToScreenCoords(_camId, (lat, lon));
            return point;
            //return (100, 200); // 示例返回值
        }
    }
}
