using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTSmartClassroom
{
    internal class JsonStruture
    {
        public class HardwareInfo
        {
            public string Computer {  get; set; }
            public string Name { get; set; }
            public List<SensorInfo> Sensors { get; set; } = new List<SensorInfo>();

            public DateTime Timestamp { get; set; }
        }

        public class SensorInfo
        {
            public string Type { get; set; }
            public string Name { get; set; }
            public double? Value { get; set; }
            public DateTime Timestamp { get; set; }

        }
    }
}
