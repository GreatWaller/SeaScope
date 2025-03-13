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


        public static (float X, float Y) ConvertToScreenCoords(string deviceId, (double Lat, double Lon) shipLoc)
        {
            if (!cameraInfoDict.TryGetValue(deviceId, out var cameraInfo))
            {
                Console.WriteLine($"Camera with DeviceId {deviceId} not found.");
                return (-1,-1);
            }

            if (!cameraIntrinsics.TryGetValue(deviceId, out var K))
            {
                Console.WriteLine($"Intrinsics not found for DeviceId {deviceId}.");
                return (-1, -1);
            }
            var translation = CameraProjection.ComputeTranslation(cameraInfo.Latitude,cameraInfo.Longitude,cameraInfo.Altitude, shipLoc.Lat, shipLoc.Lon,0);

            // 获取相机状态
            CameraStatus status = cameraService.GetCurrentStatus(cameraInfo.DeviceId);
            Console.WriteLine($"CameraStatus: {status.PanPosition}, {status.TiltPosition}, {status.ZoomPosition}");

            // 获取相机安装位置的倾角
            float installationTilt = cameraInfo.HomeTiltToHorizon;

            // 计算旋转矩阵
            // 计算旋转矩阵
            var tilt = status.TiltPosition + installationTilt;
            var pan = -status.PanPosition;
            if (pan > 90 || pan < -90)
            {
                tilt = -tilt;
            }
            var R = CameraProjection.ComputeRotationMatrix(tilt, pan, 0);

            // 更新内参矩阵
            var newK = CameraProjection.UpdateIntrinsics(K, status.ZoomPosition);

            // 投影点到相机坐标
            Point point = CameraProjection.ProjectPoint(new Vector3((float)translation.X, (float)translation.Y, (float)translation.Z), newK, R);
            var uv = ((float)point.X/ cameraInfo.VideoWidth, (float)point.Y/cameraInfo.VideoHeight);
            //Console.WriteLine($"Projected Point: {uv.X}, {uv.Y}");
            return uv;

        }
    }
}
