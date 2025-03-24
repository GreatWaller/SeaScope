namespace SeaScope.Models
{
    public class AISDataBase
    {
        public string Lat { get; set; } // 纬度（字符串格式）
        public string Lon { get; set; } // 经度（字符串格式）
        public string Mmsi { get; set; } // MMSI号
        public string Name { get; set; }
        public string Time { get; set; } // 时间戳

        // 辅助方法：将字符串经纬度转换为double
        public double Latitude => double.TryParse(Lat, out var lat) ? lat : 0.0;
        public double Longitude => double.TryParse(Lon, out var lon) ? lon : 0.0;
    }
}
