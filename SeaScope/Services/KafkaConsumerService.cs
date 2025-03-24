using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using SeaScope.Models;
using SeaScope.Utilities;
using System.Collections.Concurrent;

namespace SeaScope.Services
{
    public class KafkaConsumerService : IKafkaConsumerService
    {
        private readonly IProjectionService _projectionService;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly IConfiguration _config;
        public ConcurrentDictionary<string, (double Lat, double Lon)> ActiveCameras { get; } = new();
        private bool _isPaused = false;
        private IConsumer<string, string> _consumer; // 修改为带 key 的类型
        private readonly double MaxDistanceNm = 5000;
        private readonly string _targetKey; // 新增字段存储目标 key

        public KafkaConsumerService(
            IProjectionService projectionService,
            ILogger<KafkaConsumerService> logger,
            IConfiguration config)
        {
            _projectionService = projectionService;
            _logger = logger;
            _config = config;
            MaxDistanceNm = _config.GetValue<double>("MaxDistanceNm");
            _targetKey = _config.GetValue<string>("Kafka:TargetKey"); // 从配置中读取 key，可能为空

            string baseUri = _config.GetValue<string>("CameraConfig:BaseUri") ?? "https://192.168.1.42:44311/api/services/app/";
            string configFilePath = _config.GetValue<string>("CameraConfig:ConfigFilePath") ?? "camera_config.json";
            var cameraController = new CameraController(baseUri, configFilePath);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var kafkaConfig = new ConsumerConfig
            {
                BootstrapServers = _config.GetValue<string>("Kafka:Consumer:BootstrapServers"),
                GroupId = _config.GetValue<string>("Kafka:Consumer:GroupId"),
                AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_config.GetValue<string>("Kafka:Consumer:AutoOffsetReset")),
                EnableAutoCommit = _config.GetValue<bool>("Kafka:Consumer:EnableAutoCommit", false),
                SessionTimeoutMs = _config.GetValue<int>("Kafka:Consumer:SessionTimeoutMs", 30000),
                MaxPollIntervalMs = _config.GetValue<int>("Kafka:Consumer:MaxPollIntervalMs", 300000)
            };

            // 使用带 key 的消费者
            _consumer = new ConsumerBuilder<string, string>(kafkaConfig).Build();
            string topic = _config.GetValue<string>("Kafka:Topic");
            _consumer.Subscribe(topic);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                try
                {
                    var message = _consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (message != null)
                    {
                        // 如果配置了 TargetKey，则进行 key 筛选；否则处理所有消息
                        if (!string.IsNullOrEmpty(_targetKey))
                        {
                            if (message.Message.Key == _targetKey)
                            {
                                var aisData = AISParser.ParseWithKey(message.Message.Value);
                                ProcessAISData(aisData.Brf);
                            }
                            else
                            {
                                _logger.LogDebug($"Skipping message with key: {message.Message.Key}, expected: {_targetKey}");
                            }
                        }
                        else
                        {
                            // key 为空时，按原有逻辑处理所有消息
                            var aisData = AISParser.Parse(message.Message.Value);
                            ProcessAISData(aisData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming Kafka message");
                }
            }

            _consumer.Close();
        }

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;

        private void ProcessAISData(AISDataBase aisData)
        {
            foreach (var (camId, camLoc) in ActiveCameras)
            {
                double distance = GeoCalculator.ComputeDistance(
                    (aisData.Latitude, aisData.Longitude),
                    (camLoc.Lat, camLoc.Lon));

                double d = CoordinateConverter.ComputeDistance((camLoc.Lat, camLoc.Lon), (aisData.Latitude, aisData.Longitude));
                if (distance <= MaxDistanceNm)
                {
                    Console.WriteLine($"Camera: {camId}; MMSI: {aisData.Mmsi}; Name:{aisData.Name}; ShipGeo: {aisData.Latitude},{aisData.Longitude}; Distance: {distance}:{d}");
                    _projectionService.AddShip(camId, aisData.Mmsi, aisData.Latitude, aisData.Longitude);
                }
            }
        }
    }
}
