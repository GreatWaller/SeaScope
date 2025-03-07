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
        private IConsumer<Ignore, string> _consumer;
        private readonly double MaxDistanceNm = 5000;

        public KafkaConsumerService(
            IProjectionService projectionService,
            ILogger<KafkaConsumerService> logger,
            IConfiguration config) // 从配置中读取相机位置
        {
            _projectionService = projectionService;
            _logger = logger;
            _config = config;
            MaxDistanceNm = _config.GetValue<double>("MaxDistanceNm");

            // 从配置中读取相机相关参数
            string baseUri = _config.GetValue<string>("CameraConfig:BaseUri") ?? "https://192.168.1.42:44311/api/services/app/";
            string configFilePath = _config.GetValue<string>("CameraConfig:ConfigFilePath") ?? "camera_config.json";
            var cameraController = new CameraController(baseUri, configFilePath);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // 从配置中读取 Kafka 配置
            var kafkaConfig = new ConsumerConfig
            {
                BootstrapServers = _config.GetValue<string>("Kafka:Consumer:BootstrapServers"),
                GroupId = _config.GetValue<string>("Kafka:Consumer:GroupId"),
                AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_config.GetValue<string>("Kafka:Consumer:AutoOffsetReset")),
                EnableAutoCommit = _config.GetValue<bool>("Kafka:Consumer:EnableAutoCommit", false),
                SessionTimeoutMs = _config.GetValue<int>("Kafka:Consumer:SessionTimeoutMs", 30000),
                MaxPollIntervalMs = _config.GetValue<int>("Kafka:Consumer:MaxPollIntervalMs", 300000)
            };

            _consumer = new ConsumerBuilder<Ignore, string>(kafkaConfig).Build();
            string topic = _config.GetValue<string>("Kafka:Topic");
            _consumer.Subscribe(topic);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    await Task.Delay(1000, cancellationToken); // 暂停时降低资源占用
                    continue;
                }

                try
                {
                    var message = _consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (message != null)
                    {
                        var aisData = AISParser.Parse(message.Message.Value);
                        ProcessAISData(aisData);
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

        private void ProcessAISData(AISData aisData)
        {
            foreach (var (camId, camLoc) in ActiveCameras)
            {
                double distance = GeoCalculator.ComputeDistance(
                    (aisData.Latitude, aisData.Longitude),
                    (camLoc.Lat, camLoc.Lon));

                double d = CoordinateConverter.ComputeDistance((camLoc.Lat, camLoc.Lon), (aisData.Latitude, aisData.Longitude));
                Console.WriteLine($"Camera: {camId}; ShipGeo: {aisData.Latitude},{aisData.Longitude}; Distance: {distance}:{d}");
                if (distance <= MaxDistanceNm)
                {
                    _projectionService.AddShip(camId, aisData.Mmsi, aisData.Latitude, aisData.Longitude);
                }
            }
        }
    }
}
