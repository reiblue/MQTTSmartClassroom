using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace MQTTSmartClassroom
{
    public class DiskIoMonitor
    {
        private readonly PerformanceCounter _read;
        private readonly PerformanceCounter _write;
        private double maxReadMBs = 0;
        private double maxWriteMBs = 0;

        
        public DiskIoMonitor(string instanceName = "_Total")
        {
            // Instâncias comuns: "_Total", "0 C:", "1 D:", etc. (depende do Windows)
            _read = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", instanceName);
            _write = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", instanceName);

            // "aquecimento": a primeira leitura costuma vir 0
            _read.NextValue();
            _write.NextValue();
        }

        public (double readMBs, double writeMBs, double maxRead, double maxWrite) ReadNow()
        {
            double readBps = _read.NextValue();
            double writeBps = _write.NextValue();

            double readMBs = readBps / 1024.0 / 1024.0;
            double writeMBs = writeBps / 1024.0 / 1024.0;

            // 👇 registra o pico máximo observado
            if (readMBs > maxReadMBs) maxReadMBs = readMBs;
            if (writeMBs > maxWriteMBs) maxWriteMBs = writeMBs;

            return (readMBs, writeMBs, maxReadMBs, maxWriteMBs);
        }
    }
}
