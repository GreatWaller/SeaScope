namespace SeaScope.Models
{
    public class AISData
    {
        public string Code { get; set; }
        public string Cog { get; set; } // 航向（Course Over Ground）
        public int Enc { get; set; }   // 加密标志？
        public string From { get; set; } // 来源
        public string Hdg { get; set; } // 船首向（Heading）
        public string Id { get; set; }  // 消息ID
        public string Lat { get; set; } // 纬度（字符串格式）
        public string Lon { get; set; } // 经度（字符串格式）
        public string Mmsi { get; set; } // MMSI号
        public string Msg { get; set; } // 消息内容
        public string MsgId { get; set; } // 消息类型ID
        public string Rot { get; set; } // 转向率（Rate of Turn）
        public string SailStatus { get; set; } // 航行状态
        public string Speed { get; set; } // 速度
        public string Time { get; set; } // 时间戳

        // 辅助方法：将字符串经纬度转换为double
        public double Latitude => double.TryParse(Lat, out var lat) ? lat : 0.0;
        public double Longitude => double.TryParse(Lon, out var lon) ? lon : 0.0;
    }
}
