using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTSmartClassroom
{
    public class ComputerInfo
    {
        public string ComputerName { get; set; }
        public List<ProcessInfo> ProcessList { get; set; }
    }
}
