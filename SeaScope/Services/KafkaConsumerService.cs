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

            MaxDistanceNm = config.GetValue<double>("MaxDistanceNm");

            string baseUri = "https://192.168.1.42:44311/api/services/app/";

            string configFilePath = "camera_config.json";
            var cameraController = new CameraController(baseUri, configFilePath);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = "192.168.1.75:9092",
                GroupId = "ais-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            _consumer.Subscribe("deviceAisDymamicTopic");

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
