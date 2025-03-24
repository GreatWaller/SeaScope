namespace SeaScope.Models
{
    public class ProcessedAISData
    {
        public int MsgType { get; set; }
        public BrfData Brf { get; set; }
    }

    public class BrfData: AISDataBase
    {
        public string SourceId { get; set; }
        public string Batch { get; set; }
        public string UniqueSign { get; set; }
        public string SourceIp { get; set; }
        public string BSName { get; set; }
        public string Enc { get; set; }
        public string ShipId { get; set; }
        public int BdCard { get; set; }
        public string TerminalNo { get; set; }
        public int TerminalType { get; set; }
        public string GlobalId { get; set; }
        public int FuseFlags { get; set; }
        public int NavStatus { get; set; }
        public string RegionId { get; set; }
        public double Alt { get; set; }
        public double Sog { get; set; }
        public double Cog { get; set; }
        public double Rot { get; set; }
        public double Heading { get; set; }
        public long Time { get; set; }
        public int TrackPtCnt { get; set; }
        public int CountFlags { get; set; }
    }
}
