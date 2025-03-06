namespace SeaScope.Utilities
{
    public static class GeoCalculator
    {
        const double EarthRadiusNm = 3440.1; // 地球半径（海里）

        public static double CalculateDistance((double Lat, double Lon) point1, (double Lat, double Lon) point2)
        {
            double ToRadians(double degrees) => degrees * Math.PI / 180;

            var lat1 = ToRadians(point1.Lat);
            var lon1 = ToRadians(point1.Lon);
            var lat2 = ToRadians(point2.Lat);
            var lon2 = ToRadians(point2.Lon);

            var dLat = lat2 - lat1;
            var dLon = lon2 - lon1;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusNm * c;
        }

        public static double ComputeDistance((double Lat, double Lon) point1, (double Lat, double Lon) point2)
        {
            const double R = 6371000; // 地球半径（米）

            double radLat1 = Math.PI * point1.Lat / 180.0;
            double radLat2 = Math.PI * point2.Lat / 180.0;
            double deltaLat = radLat2 - radLat1;
            double deltaLon = Math.PI * (point2.Lon - point1.Lon) / 180.0;

            double a = Math.Pow(Math.Sin(deltaLat / 2), 2) +
                       Math.Cos(radLat1) * Math.Cos(radLat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // 返回两点之间的直线距离（米）
        }

    }
}
