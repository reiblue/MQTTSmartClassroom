using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace MQTTSmartClassroom
{
    public class DiskStatus
    {

        public string COMPUTER  { get; set; }
        public double totalSize  { get; set; }
        public double freeSpace  { get; set; }
        public double usedSpace  { get; set; }
        public double usedPercentage  { get; set; }
     }
}
