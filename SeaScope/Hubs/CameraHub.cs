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

        public CameraHub(IKafkaConsumerService kafkaService)
        {
            _kafkaService = kafkaService;
        }

        public async Task SelectCamera(string camId)
        {
            // 获取当前客户端的旧组（如果存在）
            if (_clientGroups.TryGetValue(Context.ConnectionId, out var oldCamId))
            {
                // 从旧组中移除当前客户端
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldCamId);
            }

            // 将当前客户端加入新组
            await Groups.AddToGroupAsync(Context.ConnectionId, camId);

            // 更新客户端的组状态
            _clientGroups[Context.ConnectionId] = camId;
        }

        public override async Task OnConnectedAsync()
        {
            if (Interlocked.Increment(ref _activeClients) == 1)
            {
                _kafkaService.Resume();
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // 客户端断开时清理组成员关系
            if (_clientGroups.TryRemove(Context.ConnectionId, out var oldCamId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldCamId);
            }

            if (Interlocked.Decrement(ref _activeClients) == 0)
            {
                _kafkaService.Pause();
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
