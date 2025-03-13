using System.Numerics;
using System;
using OpenCvSharp;

namespace SeaScope.Utilities
{
    public class CameraProjection
    {
        // 计算外参：平移向量 t
        public static (double X, double Y, double Z) ComputeTranslation(double latCam, double lonCam, double altCam, double latShip, double lonShip, double altShip)
        {
            var translationXY = CoordinateConverter.GetRelativePosition(lonCam, latCam, lonShip, latShip);
            var translationZ = altShip - altCam;
            return (translationXY[0], translationXY[1], translationZ);
        }

        // 计算旋转矩阵 R
        public static Mat ComputeRotationMatrix(float roll, float pitch, float yaw)
        {
            // 角度转换为弧度
            double radRoll = roll * Math.PI / 180.0;
            double radPitch = pitch * Math.PI / 180.0;
            double radYaw = yaw * Math.PI / 180.0;

            // 创建 X 轴旋转矩阵
            Mat Rx = Mat.FromPixelData(3, 3, MatType.CV_32F, new float[]
            {
            1, 0, 0,
            0, (float)Math.Cos(radRoll), -(float)Math.Sin(radRoll),
            0, (float)Math.Sin(radRoll), (float)Math.Cos(radRoll)
            });

            // 创建 Y 轴旋转矩阵
            Mat Ry = Mat.FromPixelData(3, 3, MatType.CV_32F, new float[]
            {
            (float)Math.Cos(radPitch), 0, (float)Math.Sin(radPitch),
            0, 1, 0,
            -(float)Math.Sin(radPitch), 0, (float)Math.Cos(radPitch)
            });

            // 创建 Z 轴旋转矩阵
            Mat Rz = Mat.FromPixelData(3, 3, MatType.CV_32F, new float[]
            {
            (float)Math.Cos(radYaw), -(float)Math.Sin(radYaw), 0,
            (float)Math.Sin(radYaw), (float)Math.Cos(radYaw), 0,
            0, 0, 1
            });

            // 计算最终旋转矩阵 R = Rz * Ry * Rx
            Mat R = Rz * Ry * Rx;
            return R;
        }

        public static Point ProjectPoint(Vector3 shipUTM, Mat K, Mat R, Mat t)
        {
            Mat worldMatrix = Mat.FromPixelData(3, 1, MatType.CV_32F, new float[]
            {
                shipUTM.X, shipUTM.Y, shipUTM.Z
            });

            // 计算摄像机坐标 P_camera = [R | t] * P_world
            //Mat extrinsicMatrix = new Mat(3, 4, MatType.CV_32F);
            //R.CopyTo(extrinsicMatrix.ColRange(0, 3));
            //t.CopyTo(extrinsicMatrix.ColRange(3, 4));
            //Console.WriteLine("ExtrinsicMatrix:");

            //for (int i = 0; i < 3; i++)
            //{
            //    for (int j = 0; j < 4; j++)
            //    {
            //        Console.Write($"{extrinsicMatrix.At<float>(i, j):F6} ");
            //    }
            //    Console.WriteLine();
            //}
            Mat cameraPoint = R * worldMatrix.RowRange(0, 3);
            Console.WriteLine("cameraPoint:");

            for (int i = 0; i < 3; i++)
            {
                Console.Write($"{cameraPoint.At<float>(i, 0):F6} ");
            }
            Console.WriteLine();
            // 计算投影 P_image = K * P_camera
            Mat imagePoint = K * cameraPoint;
            Console.WriteLine("imagePoint:");

            for (int i = 0; i < 3; i++)
            {
                Console.Write($"{imagePoint.At<float>(i, 0):F6} ");
            }
            Console.WriteLine();
            // 归一化
            float u = imagePoint.At<float>(0, 0) / imagePoint.At<float>(2, 0);
            float v = imagePoint.At<float>(1, 0) / imagePoint.At<float>(2, 0);
            return new Point((int)u, (int)v);
        }

        public static Point ProjectPoint(Vector3 shipUTM, Mat K, Mat R)
        {
            Mat worldMatrix = Mat.FromPixelData(3, 1, MatType.CV_32F, new float[]
            {
                -shipUTM.Y, -shipUTM.Z,  shipUTM.X
            });
            Mat cameraPoint = R * worldMatrix.RowRange(0, 3);
            //Console.WriteLine("cameraPoint:");

            //for (int i = 0; i < 3; i++)
            //{
            //    Console.Write($"{cameraPoint.At<float>(i, 0):F6} ");
            //}
            //Console.WriteLine();
            // 计算投影 P_image = K * P_camera
            Mat imagePoint = K * cameraPoint;
            //Console.WriteLine("imagePoint:");

            //for (int i = 0; i < 3; i++)
            //{
            //    Console.Write($"{imagePoint.At<float>(i, 0):F6} ");
            //}
            //Console.WriteLine();
            // 归一化
            float u = imagePoint.At<float>(0, 0) / imagePoint.At<float>(2, 0);
            float v = imagePoint.At<float>(1, 0) / imagePoint.At<float>(2, 0);

            return new Point((int)u, (int)v);
        }

        // 更新内参矩阵的方法
        public static Mat UpdateIntrinsics(Mat K, float zoomFactor)
        {
            // 复制当前内参矩阵
            Mat newK = K.Clone();

            // 读取当前内参值
            float fx = (float)K.At<float>(0, 0);  // 焦距 fx
            float fy = (float)K.At<float>(1, 1);  // 焦距 fy
            float cx = (float)K.At<float>(0, 2);  // 主点 cx
            float cy = (float)K.At<float>(1, 2);  // 主点 cy

            // 计算新的焦距值（焦距随着变焦倍数缩放）
            float newFx = fx * zoomFactor;
            float newFy = fy * zoomFactor;

            // 设定新内参矩阵
            newK.Set(0, 0, newFx);  // 更新 fx
            newK.Set(1, 1, newFy);  // 更新 fy

            // 返回更新后的内参矩阵
            return newK;
        }

    }
}
