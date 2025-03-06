using CameraManager.OnvifCamera;
using Newtonsoft.Json;
using OpenCvSharp;
using SeaScope.Utilities;
using System.Numerics;

namespace SeaScope.Services
{
    public class CameraController
    {
        private static OnvifCameraService cameraService;
        private static Dictionary<string, CameraInfo> cameraInfoDict;
        private static Dictionary<string, Mat> cameraIntrinsics;

        public CameraController(string baseUrl, string configFilePath)
        {
            cameraService = new OnvifCameraService(baseUrl);
            cameraInfoDict = new Dictionary<string, CameraInfo>();
            cameraIntrinsics = LoadIntrinsicsFromConfig(configFilePath);
            var cameraInfos = cameraService.GetAllDevices();
            foreach (var camera in cameraInfos)
            {
                cameraInfoDict[camera.DeviceId] = camera;
            }
        }

        private Dictionary<string, Mat> LoadIntrinsicsFromConfig(string configFilePath)
        {
            var intrinsicsDict = new Dictionary<string, Mat>();
            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("Configuration file not found.");
                return intrinsicsDict;
            }

            string jsonContent = File.ReadAllText(configFilePath);
            var cameraConfigs = JsonConvert.DeserializeObject<Dictionary<string, float[]>>(jsonContent);

            foreach (var kvp in cameraConfigs)
            {
                if (kvp.Value.Length == 9)
                {
                    intrinsicsDict[kvp.Key] = Mat.FromArray(new float[,]
                    {
                        { kvp.Value[0], kvp.Value[1], kvp.Value[2] },
                        { kvp.Value[3], kvp.Value[4], kvp.Value[5] },
                        { kvp.Value[6], kvp.Value[7], kvp.Value[8] }
                    });
                }
            }

            return intrinsicsDict;
        }

        public static Point? ProjectToCameraCoordinates(string deviceId, Vector3 worldPosition)
        {
            if (!cameraInfoDict.TryGetValue(deviceId, out var cameraInfo))
            {
                Console.WriteLine($"Camera with DeviceId {deviceId} not found.");
                return null;
            }

            if (!cameraIntrinsics.TryGetValue(deviceId, out var K))
            {
                Console.WriteLine($"Intrinsics not found for DeviceId {deviceId}.");
                return null;
            }

            // 获取相机状态
            CameraStatus status = cameraService.GetCurrentStatus(deviceId);
            Console.WriteLine($"CameraStatus: {status.PanPosition}, {status.TiltPosition}, {status.ZoomPosition}");

            // 获取相机安装位置的倾角
            float installationTilt = cameraInfo.HomeTiltToHorizon;

            // 计算旋转矩阵
            var R = CameraProjection.ComputeRotationMatrix(status.TiltPosition + installationTilt, -status.PanPosition, 0);

            // 更新内参矩阵
            var newK = CameraProjection.UpdateIntrinsics(K, status.ZoomPosition);

            // 投影点到相机坐标
            Point uv = CameraProjection.ProjectPoint(worldPosition, newK, R);
            //Console.WriteLine($"Projected Point: {uv.X}, {uv.Y}");

            return uv;
        }

        public static (int X, int Y) ConvertToScreenCoords(string deviceId, (double X, double Y, double Z) shipLoc)
        {
            var point = ProjectToCameraCoordinates(deviceId, new Vector3((float)shipLoc.X, (float)shipLoc.Y, (float)shipLoc.Z));
            if (point != null)
            {
                return (point.Value.X, point.Value.Y);
            }
            else
            {
                return (0, 0);
            }
        }
    }
}
