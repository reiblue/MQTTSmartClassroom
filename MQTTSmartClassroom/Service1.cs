using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MQTTSmartClassroom.JsonStruture;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;
using System.IO;
using static CpuTemperatureMonitor;

namespace MQTTSmartClassroom
{
    public partial class smartclassroom : ServiceBase
    {
        private Thread workerThread;
        private bool isRunning;
        int timeSeconds = 10 * 1000;
        private string path = @"C:\Program Files\SmartClassroom\";
        string IPBroker;
        int port = 8883;
        MQTTClient.MqttPublisher broker;
        string nameCertificate;
        string smartClassroomName;
        int timeMinutes;

        public smartclassroom()
        {
            InitializeComponent();
            IPBroker = File.ReadAllText(path + "IPBroker.txt").Trim();
            nameCertificate = File.ReadAllText(path + "NameCertificate.txt").Trim();
            smartClassroomName = File.ReadAllText(path + "SmartClassroomName.txt").Trim();
            timeMinutes = int.Parse(File.ReadAllText(path + "TimerMinutes.txt").Trim());
            //System.IO.File.AppendAllText(path + "LogServico.txt", $"IP: {IPBroker}\n");
        }

        protected async override void OnStart(string[] args)
        {
            try
            {
                isRunning = true;
                workerThread = new Thread(Executar);
                workerThread.Start();

                broker =
                    new MQTTClient.MqttPublisher(
                        IPBroker,
                        port,
                        null,
                        null,
                        true,
                path + nameCertificate);

                await broker.ConnectAsync();

                timeSeconds = 1000 * 60 * timeMinutes; // Convertendo minutos para segundos


            }
            catch (Exception ex)
            {

                System.IO.File.AppendAllText("LogServico.txt",
                        $"Erro: {ex.Message}\n");

            }            
            
        }

        private async void Executar()
        {
            while (isRunning)
            {
                try
                {

                    Computer computer = new Computer
                    {
                        IsCpuEnabled = true,
                        IsGpuEnabled = true,
                        IsMemoryEnabled = true,
                        IsMotherboardEnabled = true,
                        IsStorageEnabled = true
                    };

                    computer.Open();

                    foreach (var hardware in computer.Hardware)
                    {
                        Console.WriteLine($"Hardware: {hardware.Name}");
                        hardware.Update(); // Atualiza os sensores

                        foreach (var sensor in hardware.Sensors)
                        {
                            Console.WriteLine($"  {sensor.SensorType}: {sensor.Name} = {sensor.Value} ");
                        }
                    }

                    var hardwareList = new List<HardwareInfo>();

                    foreach (var hardware in computer.Hardware)
                    {
                        hardware.Update();
                        var hardwareInfo = new HardwareInfo
                        {
                            Name = hardware.Name,
                            Computer = System.Net.Dns.GetHostName()
                        };


                        foreach (var sensor in hardware.Sensors)
                        {
                            hardwareInfo.Sensors.Add(new SensorInfo
                            {
                                Type = sensor.SensorType.ToString(),
                                Name = sensor.Name,
                                Value = sensor.Value
                            });
                        }

                        hardwareList.Add(hardwareInfo);
                    }

                    // Converter para JSON
                    string json = JsonSerializer.Serialize(hardwareList, new JsonSerializerOptions { WriteIndented = true });

                    await broker.PublishAsync(smartClassroomName + @"\HARDWARE_SENSORS", json);
                    //--------------------------------------------------------------------------------------------------------------------------
                    
                    var processos = Process.GetProcesses();
                    int cpuCores = Environment.ProcessorCount;

                    var cpuTimes = new Dictionary<int, TimeSpan>();
                    var timeStamp = DateTime.UtcNow;

                    foreach (var proc in processos)
                    {
                        try
                        {
                            cpuTimes[proc.Id] = proc.TotalProcessorTime;
                        }
                        catch { }
                    }

                    Thread.Sleep(1000);

                    var newTimeStamp = DateTime.UtcNow;
                    var processosNovos = Process.GetProcesses();

                    List<ProcessInfo> listaProcessos = new List<ProcessInfo>();

                    foreach (var proc in processosNovos)
                    {
                        try
                        {
                            if (!cpuTimes.ContainsKey(proc.Id))
                                continue;

                            TimeSpan oldCpuTime = cpuTimes[proc.Id];
                            TimeSpan newCpuTime = proc.TotalProcessorTime;

                            double cpuUsedMs = (newCpuTime - oldCpuTime).TotalMilliseconds;
                            double elapsedMs = (newTimeStamp - timeStamp).TotalMilliseconds;

                            double cpuPercent = (cpuUsedMs / elapsedMs) * 100.0 / cpuCores;

                            listaProcessos.Add(new ProcessInfo
                            {
                                PID = proc.Id,
                                CpuPercentage = Math.Round(cpuPercent, 2),
                                Name = proc.ProcessName
                            });
                        }
                        catch { }
                    }

                    // Ordena por maior uso de CPU
                    var listaOrdenada = listaProcessos.OrderByDescending(p => p.CpuPercentage).ToList();

                    var computerInfo = new ComputerInfo
                    {
                        ComputerName = Environment.MachineName,
                        ProcessList = listaOrdenada
                    };

                    // Converte para JSON
                    json = JsonSerializer.Serialize(computerInfo, new JsonSerializerOptions { WriteIndented = true });
                    await broker.PublishAsync(smartClassroomName  + @"\PROCESS_COMPUTERS", json);


                    //Temperatura
                    CpuTemperatureMonitor monitor = new CpuTemperatureMonitor();
                    TemperatureReading cpuReading = monitor.GetOverallCpuTemperatureOpenHardware();

                    json = JsonSerializer.Serialize(cpuReading, new JsonSerializerOptions { WriteIndented = true });
                    await broker.PublishAsync(smartClassroomName + @"\CPU_TEMPERATURE", json);

                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(path + "LogServico.txt",
                        $"Erro: {ex.Message}\n");
                }
                finally
                {
                    Thread.Sleep(timeSeconds);
                }
            }
        }

        protected async override void OnStop()
        {
            isRunning = false;
            await broker.DisconnectAsync();
            if (workerThread != null && workerThread.IsAlive)
                workerThread.Join();
        }
    }
}
