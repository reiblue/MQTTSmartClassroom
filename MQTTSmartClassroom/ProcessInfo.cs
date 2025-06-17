using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTSmartClassroom
{
    public class ProcessInfo
    {
        public int PID { get; set; }
        public double CpuPercentage { get; set; }
        public string Name { get; set; }
    }
}
