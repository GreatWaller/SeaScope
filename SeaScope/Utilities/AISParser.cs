using Newtonsoft.Json;
using SeaScope.Models;
using System.Text.Json;

namespace SeaScope.Utilities
{
    //public record AISData(string Mmsi, double Lat, double Lon);

    public static class AISParser
    {
        public static AISData Parse(string rawMessage)
        {
            try
            {
                return JsonConvert.DeserializeObject<AISData>(rawMessage);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Failed to parse AIS message", ex);
            }
        }
    }
}