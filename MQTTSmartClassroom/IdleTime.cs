using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace MQTTSmartClassroom
{
    public static class IdleTime
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static TimeSpan GetIdleTime()
        {
            var lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            if (!GetLastInputInfo(ref lastInput)) return TimeSpan.Zero;
            uint tickCount = (uint)Environment.TickCount;
            uint idleMillis = tickCount - lastInput.dwTime;
            return TimeSpan.FromMilliseconds(idleMillis);
        }

        public static int GetIdleSeconds() => (int)GetIdleTime().TotalSeconds;
    }
}
