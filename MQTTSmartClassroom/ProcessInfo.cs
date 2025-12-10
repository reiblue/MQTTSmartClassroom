using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MQTTSmartClassroom
{
    public class ProcessInfo
    {
        public DateTimeOffset Timestamp { get; set; }
        public int PID { get; set; }
        public double CpuPercentage { get; set; }
        public string Name { get; set; }

        public TimeSpan TotalProcessorTime { get; set; }
        public TimeSpan UserProcessorTime { get; set; }
        public TimeSpan PrivilegedProcessorTime { get; set; }

        // --- Memória ---
        public long WorkingSet64 { get; set; }                // Memória física em bytes
        public long PrivateMemorySize64 { get; set; }
        public long VirtualMemorySize64 { get; set; }
        public long PagedMemorySize64 { get; set; }
        public long PeakWorkingSet64 { get; set; }
        public long PeakPagedMemorySize64 { get; set; }
        public long PeakVirtualMemorySize64 { get; set; }

        // --- Threads e módulos ---
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }

        // --- Informações de execução ---
        public DateTime? StartTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public bool HasExited { get; set; }
        public int? ExitCode { get; set; }

        // --- Sistema ---
        public string MachineName { get; set; }
        public int SessionId { get; set; }
        public ProcessPriorityClass PriorityClass { get; set; }
        public int BasePriority { get; set; }

        [JsonIgnore]
        public IntPtr ProcessorAffinity { get; set; }

        public long ProcessorAffinityValue
        {
            get => ProcessorAffinity.ToInt64();
            set => ProcessorAffinity = new IntPtr(value);
        }


        // --- Informações de E/S ---
        public long TotalProcessorTicks => TotalProcessorTime.Ticks;
        public long NonpagedSystemMemorySize64 { get; set; }
        public long PagedSystemMemorySize64 { get; set; }

        // --- Extras ---
        public bool Responding { get; set; }
        public string MainModuleFileName { get; set; }
        public string MainModulePath { get; set; }
        public string MainWindowTitle { get; private set; }

        // Helper para ler propriedades que às vezes disparam exceção (processos do sistema / outras sessões)
        public static T SafeGet<T>(Func<T> getter)
        {
            try { return getter(); }
            catch { return default; }
        }

        // Se quiser, centralize o mapeamento num método:
        public static ProcessInfo MapProcessToInfo(Process proc, double cpuPercent, DateTimeOffset ts)
        {
            return new ProcessInfo
            {
                // básicos
                Timestamp = ts,
                PID = SafeGet(() => proc.Id),
                Name = SafeGet(() => proc.ProcessName),
                MainWindowTitle = SafeGet(() => proc.MainWindowTitle),

                // CPU
                CpuPercentage = Math.Round(cpuPercent, 2),
                TotalProcessorTime = SafeGet(() => proc.TotalProcessorTime),
                UserProcessorTime = SafeGet(() => proc.UserProcessorTime),
                PrivilegedProcessorTime = SafeGet(() => proc.PrivilegedProcessorTime),

                // Memória
                WorkingSet64 = SafeGet(() => proc.WorkingSet64),
                PrivateMemorySize64 = SafeGet(() => proc.PrivateMemorySize64),
                VirtualMemorySize64 = SafeGet(() => proc.VirtualMemorySize64),
                PagedMemorySize64 = SafeGet(() => proc.PagedMemorySize64),
                PeakWorkingSet64 = SafeGet(() => proc.PeakWorkingSet64),
                PeakPagedMemorySize64 = SafeGet(() => proc.PeakPagedMemorySize64),
                PeakVirtualMemorySize64 = SafeGet(() => proc.PeakVirtualMemorySize64),
                NonpagedSystemMemorySize64 = SafeGet(() => proc.NonpagedSystemMemorySize64),
                PagedSystemMemorySize64 = SafeGet(() => proc.PagedSystemMemorySize64),

                // Threads / handles
                ThreadCount = SafeGet(() => proc.Threads.Count),
                HandleCount = SafeGet(() => proc.HandleCount),

                // Execução
                HasExited = SafeGet(() => proc.HasExited),
                StartTime = SafeGet(() => proc.StartTime),
                ExitTime = SafeGet(() => proc.ExitTime),
                ExitCode = SafeGet(() => proc.ExitCode),

                // Sistema
                MachineName = SafeGet(() => proc.MachineName),
                SessionId = SafeGet(() => proc.SessionId),
                PriorityClass = SafeGet(() => proc.PriorityClass),
                BasePriority = SafeGet(() => proc.BasePriority),
                ProcessorAffinity = SafeGet(() => proc.ProcessorAffinity),

                // Outros
                Responding = SafeGet(() => proc.Responding),
                MainModuleFileName = SafeGet(() => proc.MainModule?.FileName),
                MainModulePath = SafeGet(() => proc.MainModule?.ModuleName)
            };
        }



    }


}
