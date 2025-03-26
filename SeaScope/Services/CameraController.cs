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


        public static (float X, float Y) ConvertToScreenCoords(string deviceId, double Lat, double Lon)
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
            var translation = CameraProjection.ComputeTranslation(cameraInfo.Latitude,cameraInfo.Longitude,cameraInfo.Altitude, Lat, Lon,0);

            // 获取相机状态
            CameraStatus status = cameraService.GetCurrentStatus(cameraInfo.DeviceId);
            Console.WriteLine($"CameraStatus: {status.PanPosition}, {status.TiltPosition}, {status.ZoomPosition}");

            // 获取相机安装位置的倾角
            float installationTilt = cameraInfo.HomeTiltToHorizon;
            float installationPan = cameraInfo.HomePanToEast;
            // 计算旋转矩阵
            // 计算旋转矩阵
            var tilt = status.TiltPosition + installationTilt;
            var pan = status.PanPosition + installationPan;
            if (pan < -180)
            {
                pan = 360 + pan;
            }
            else if (pan > 180)
            {
                pan = -360 + pan;
            }
            pan = -pan;
            //if (pan > 90 || pan < -90)
            //{
            //    tilt = -tilt;
            //}
            Console.WriteLine($"Camera Positon: {pan}, {tilt}");
            var R = CameraProjection.ComputeRotationMatrix(tilt, pan, 0);

            // 更新内参矩阵
            var newK = CameraProjection.UpdateIntrinsics(K, status.ZoomPosition);

            // 投影点到相机坐标
            Point point = CameraProjection.ProjectPoint(new Vector3((float)translation.X, (float)translation.Y, (float)translation.Z), newK, R);
            var uv = ((float)point.X/ cameraInfo.VideoWidth, (float)point.Y/cameraInfo.VideoHeight);
            //Console.WriteLine($"Projected Point: {uv.X}, {uv.Y}");
            return uv;

        }

        public static (float X, float Y) ConvertToScreenCoords2(string deviceId, double Lat, double Lon, double targetZ=0)
        {
            if (!cameraInfoDict.TryGetValue(deviceId, out var cameraInfo))
            {
                Console.WriteLine($"Camera with DeviceId {deviceId} not found.");
                return (-1, -1);
            }
            var translation = CameraProjection.ComputeTranslation(cameraInfo.Latitude, cameraInfo.Longitude, cameraInfo.Altitude, Lat, Lon, targetZ);
            var targetX = translation.X;
            var targetY = translation.Y;
            Console.WriteLine($"Target: {targetX}, {targetY}");
            // 获取相机状态
            CameraStatus status = cameraService.GetCurrentStatus(deviceId);
            Console.WriteLine($"CameraStatus: {status.PanPosition}, {status.TiltPosition}, {status.ZoomPosition}");

            // 相机参数
            double camLat = cameraInfo.Latitude;
            double camLon = cameraInfo.Longitude;
            int screenWidth = cameraInfo.VideoWidth;       // 屏幕宽度 (像素)
            int screenHeight = cameraInfo.VideoHeight;      // 屏幕高度 (像素)
            double camHeading = status.PanPosition + cameraInfo.HomePanToEast; // 水平朝向 (度)
            double camPitch = status.TiltPosition + cameraInfo.HomeTiltToHorizon;     // 俯仰角 (度)
            double camHeight = cameraInfo.Altitude - targetZ;   // 相机高度 (米)

            // 变焦相机参数
            double sensorWidth = cameraInfo.CCDWidth;   // 传感器宽度 (毫米)
            double sensorHeight = cameraInfo.CCDHeight; // 传感器高度 (毫米)
            double focalLength = cameraInfo.FocalLength * status.ZoomPosition;   // 当前焦距 (毫米)

            // 计算动态 FOV
            double horizontalFov = 2 * Math.Atan(sensorWidth / (2 * focalLength)) * 180 / Math.PI;
            double verticalFov = 2 * Math.Atan(sensorHeight / (2 * focalLength)) * 180 / Math.PI;

            // Step 1: 计算距离和方位角
            double distanceNm = Math.Sqrt(Math.Pow(targetX, 2) + Math.Pow(targetY, 2));
            double bearing = -Math.Atan2(targetY, targetX) * 180 / Math.PI;

            // Step 2: 水平方向 (X)
            double relativeBearing = bearing - camHeading;
            if (relativeBearing > 180) relativeBearing -= 360;
            if (relativeBearing < -180) relativeBearing += 360;

            double horizontalAngleRange = horizontalFov / 2.0;
            double xNormalized = relativeBearing / horizontalAngleRange; // [-1, 1]
            float x = (float)((xNormalized + 1) / 2 * screenWidth);

            // Step 3: 垂直方向 (Y) - 考虑俯仰角和动态 FOV
            double distanceMeters = distanceNm;
            double verticalAngle = Math.Atan2(camHeight, distanceMeters) * 180 / Math.PI;
            double relativeVerticalAngle = verticalAngle - camPitch;

            double verticalAngleRange = verticalFov / 2.0;
            double yNormalized = relativeVerticalAngle / verticalAngleRange; // [-1, 1]
            float y = (float)((yNormalized + 1) / 2 * screenHeight);

            // Step 4: 视野边界检查
            //if (Math.Abs(relativeBearing) > horizontalAngleRange ||
            //    Math.Abs(relativeVerticalAngle) > verticalAngleRange)
            //{
            //    return (-1, -1); // 超出水平或垂直视野
            //}

            //x = Math.Clamp(x, 0, screenWidth - 1);
            //y = Math.Clamp(y, 0, screenHeight - 1);
            //return (x, y);

            var uv = (x / screenWidth, y / screenHeight);
            //Console.WriteLine($"Projected Point: {uv.X}, {uv.Y}");
            return uv;
        }
    }
}
