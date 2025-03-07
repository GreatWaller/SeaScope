using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using SeaScope.Services;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SeaScope.Hubs
{
    public class CameraHub : Hub
    {
        private readonly IKafkaConsumerService _kafkaService;
        private static int _activeClients = 0;
        private static readonly ConcurrentDictionary<string, string> _clientGroups = new();
        private static Dictionary<string, (double Lat, double Lon)> _cameraLocations;
        public CameraHub(IKafkaConsumerService kafkaService, IConfiguration config)
        {
            _kafkaService = kafkaService;
            var section = config.GetSection("CameraLocations");
            _cameraLocations = section.GetChildren()
                .ToDictionary(
                    x => x.Key,  // 获取键 (如 "cam1", "cam2")
                    x => (       // 解析值
                        Lat: x.GetValue<double>("Lat"),
                        Lon: x.GetValue<double>("Lon")
                    )
                );
        }

        public async Task SelectCamera(string camId)
        {
            if (_clientGroups.TryGetValue(Context.ConnectionId, out var oldCamId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldCamId);
                if (ClientsInGroup(oldCamId))
                {
                    _kafkaService.ActiveCameras.TryRemove(oldCamId, out _);
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, camId);
            _clientGroups[Context.ConnectionId] = camId;

            if (!_kafkaService.ActiveCameras.ContainsKey(camId))
            {
                if (_cameraLocations?.ContainsKey(camId) == true)
                {
                    _kafkaService.ActiveCameras[camId] = _cameraLocations[camId];
                }
            }
        }

        public override async Task OnConnectedAsync()
        {
            if (Interlocked.Increment(ref _activeClients) == 1)
            {
                _kafkaService.Resume();
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_clientGroups.TryRemove(Context.ConnectionId, out var oldCamId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldCamId);
                if (!ClientsInGroup(oldCamId))
                {
                    _kafkaService.ActiveCameras.TryRemove(oldCamId, out _);
                }
            }

            if (Interlocked.Decrement(ref _activeClients) == 0)
            {
                _kafkaService.Pause();
            }
            await base.OnDisconnectedAsync(exception);
        }

        private bool ClientsInGroup(string camId)
        {
            return _clientGroups.Values.Any(g => g == camId);
        }

        // 新增：检查某个相机组是否有客户端
        public static bool HasClientsInGroup(string camId)
        {
            return _clientGroups.Values.Any(g => g == camId);
        }
    }
}
