using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using static MQTTSmartClassroom.JsonStruture;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;
using System.IO;
using System.Net;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
        int idleMaxMinutes;
        static string logPathLog;
        static readonly IPAddress BindAddress = IPAddress.Loopback; // 127.0.0.1
        const int Port = 9777;
        static readonly string CLIENT_EXE_PATH =
            Path.GetFullPath(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,
                "SmartLabKeepToAwake.exe"));

        // Cooldown entre tentativas de abrir o cliente
        static readonly TimeSpan LaunchCooldown = TimeSpan.FromSeconds(15);

        public smartclassroom()
        {
            InitializeComponent();
            try
            {
                logPathLog = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "LogServico.txt");
                IPBroker = File.ReadAllText(path + "IPBroker.txt").Trim();
                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" LOAD: IPBroker\n");

                nameCertificate = File.ReadAllText(path + "NameCertificate.txt").Trim();
                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" LOAD: nameCertificate\n");
                smartClassroomName = File.ReadAllText(path + "SmartClassroomName.txt").Trim();
                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" LOAD: smartClassroomName\n");
                timeMinutes = int.Parse(File.ReadAllText(path + "TimerMinutes.txt").Trim());
                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" LOAD: timeMinutes\n");
                idleMaxMinutes = int.Parse(File.ReadAllText(path + "IdleMaxMinutes.txt").Trim());
                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" LOAD: idleMaxMinutes\n");

            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" Contruction->Erro: {ex.Message}\n");
            }     
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
                

                timeSeconds = 1000 * 60 * timeMinutes; // Convertendo minutos para segundos

                await ConnectionTCP();

            }
            catch (Exception ex)
            {

                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" OnStart->Erro: {ex.Message}\n");

            }            
            
        }

        private async void Executar()
        {
            while (isRunning)
            {
                try
                {

                    await broker.ConnectAsync();

                    DateTime currentTime = DateTime.Now;

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

                        /*foreach (var sensor in hardware.Sensors)
                        {
                            Console.WriteLine($"  {sensor.SensorType}: {sensor.Name} = {sensor.Value} ");
                        }*/
                    }

                    var hardwareList = new List<HardwareInfo>();

                    

                    foreach (var hardware in computer.Hardware)
                    {
                        hardware.Update();
                        var hardwareInfo = new HardwareInfo
                        {
                            Name = hardware.Name,
                            Computer = System.Net.Dns.GetHostName(),
                            Timestamp = currentTime // Adiciona o timestamp atual
                        };


                        foreach (var sensor in hardware.Sensors)
                        {
                            hardwareInfo.Sensors.Add(new SensorInfo
                            {
                                Type = sensor.SensorType.ToString(),
                                Name = sensor.Name,
                                Value = sensor.Value,
                                Timestamp = currentTime // Adiciona o timestamp atual
                            });

                            
                        }

                        hardwareList.Add(hardwareInfo);
                    }
                    

                    // Converter para JSON
                    string json = JsonSerializer.Serialize(hardwareList, new JsonSerializerOptions { WriteIndented = true });

                    await broker.PublishAsync(smartClassroomName + @"/HARDWARE_SENSORS", json);
                    //--------------------------------------------------------------------------------------------------------------------------
                    
                    var processos = Process.GetProcesses();
                    int cpuCores = Environment.ProcessorCount;

                    var cpuTimes = new Dictionary<int, TimeSpan>();
                    var timeStamp = currentTime;

                    foreach (var proc in processos)
                    {
                        try
                        {
                            cpuTimes[proc.Id] = proc.TotalProcessorTime;
                        }
                        catch { }
                    }


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
                                Name = proc.ProcessName,
                                Timestamp = currentTime

                            });
                        }
                        catch { }
                    }

                    // Ordena por maior uso de CPU
                    var listaOrdenada = listaProcessos.OrderByDescending(p => p.CpuPercentage).ToList();

                    var computerInfo = new ComputerInfo
                    {
                        ComputerName = Environment.MachineName,
                        ProcessList = listaOrdenada,
                        Timestamp = currentTime
                        
                    };

                    // Converte para JSON
                    json = JsonSerializer.Serialize(computerInfo, new JsonSerializerOptions { WriteIndented = true });
                    await broker.PublishAsync(smartClassroomName  + @"/PROCESS_COMPUTERS", json);


                    /*//Temperatura
                    CpuTemperatureMonitor monitor = new CpuTemperatureMonitor();
                    TemperatureReading cpuReading = monitor.GetOverallCpuTemperatureOpenHardware();

                    json = JsonSerializer.Serialize(cpuReading, new JsonSerializerOptions { WriteIndented = true });
                    await broker.PublishAsync(smartClassroomName + @"\CPU_TEMPERATURE", json);*/

                    

                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now +  $" Running->Erro: {ex.Message}\n");
                }
                finally
                {
                    Thread.Sleep(timeSeconds);
                    await broker.DisconnectAsync();
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

        static async Task ConnectionTCP()
        {
            //Console.Title = "LoopbackServer";
            //Console.OutputEncoding = Encoding.UTF8;

            var cts = new CancellationTokenSource();

            var listener = new TcpListener(BindAddress, Port);
            listener.Start();
            System.IO.File.AppendAllText(logPathLog,
                                   DateTime.Now + $" [SERVER] Escutando em {BindAddress}:{Port} (Ctrl+C para sair)\n");

            var clients = new ConcurrentDictionary<TcpClient, NetworkStream>();



            // tarefa para aceitar conexões
            var acceptTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        client.NoDelay = true;
                        var stream = client.GetStream();
                        clients[client] = stream;

                        _ = HandleClientAsync(client, stream, clients, cts.Token);
                        System.IO.File.AppendAllText(logPathLog,
                                   DateTime.Now + $" [SERVER] Cliente conectado: {client.Client.RemoteEndPoint}\n");

                        if (clients.Count > 1)
                        {
                            SendTextAsync(client.GetStream(), "close", cts.Token).Wait();
                            System.IO.File.AppendAllText(logPathLog,
                                   DateTime.Now + $" [SERVER] Cliente desconectado (apenas 1 permitido).\n");
                            client.Close();
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        System.IO.File.AppendAllText(logPathLog,
                                   DateTime.Now + $" [SERVER] Erro Accept: {ex.Message}\n");
                    }
                }
            }, cts.Token);

            // tarefa de broadcast opcional (ping a cada 5s)
            var broadcastTask = Task.Run(async () =>
            {
                var enc = new UTF8Encoding(false);
                while (!cts.IsCancellationRequested)
                {
                    var msg = $"server_heartbeat {DateTime.Now:HH:mm:ss}\n";
                    var bytes = enc.GetBytes(msg);
                    foreach (var kv in clients.ToArray())
                    {
                        try
                        {
                            await kv.Value.WriteAsync(bytes, 0, bytes.Length, cts.Token);
                            await kv.Value.FlushAsync(cts.Token);
                        }
                        catch
                        {
                            // desconectar silenciosamente; loop de cliente cuidará
                        }
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                }
            }, cts.Token);

            // === Monitor: se não houver clientes, tenta abrir o cliente ===
            var monitorTask = StartClientMonitorAsync(clients, cts.Token);

            await Task.WhenAny(acceptTask, Task.Run(() => WaitForCancel(cts.Token)));
            cts.Cancel();

            listener.Stop();
            foreach (var kv in clients)
            {
                try { kv.Value.Close(); kv.Key.Close(); } catch { }
            }

            System.IO.File.AppendAllText(logPathLog,
                                   DateTime.Now + $" [SERVER] Encerrado.\n");
        }

        static async Task SendTextAsync(NetworkStream stream, string text, CancellationToken ct)
        {
            // UTF-8 sem BOM
            var data = Encoding.UTF8.GetBytes(text + "\n");
            await stream.WriteAsync(data, 0, data.Length, ct);
            await stream.FlushAsync();
        }

        static async Task HandleClientAsync(TcpClient client, NetworkStream stream,
            ConcurrentDictionary<TcpClient, NetworkStream> clients, CancellationToken ct)
        {
            var enc = new UTF8Encoding(false);
            var buffer = new byte[4096];
            var sb = new StringBuilder();

            try
            {
                while (!ct.IsCancellationRequested && client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read <= 0) break;

                    sb.Append(enc.GetString(buffer, 0, read));

                    // protocolo: mensagens terminadas por \n
                    string line;
                    while ((line = ExtractLine(sb)) != null)
                    {
                        Console.WriteLine($"[RECV] {line}");

                        // responde (eco + ack)
                        var reply = enc.GetBytes($"ack: {line}\n");
                        await stream.WriteAsync(reply, 0, reply.Length, ct);
                        await stream.FlushAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPathLog,
                                   DateTime.Now + $" [SERVER] Erro cliente: {ex.Message}\n");
            }
            finally
            {
                clients.TryRemove(client, out _);
                try { stream.Close(); client.Close(); } catch { }
                System.IO.File.AppendAllText(logPathLog,
                                   DateTime.Now + $" [SERVER] Cliente desconectado.\n");
            }
        }

        static string ExtractLine(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '\n')
                {
                    var line = sb.ToString(0, i).TrimEnd('\r');
                    sb.Remove(0, i + 1);
                    return line;
                }
            }
            return null;
        }

        static async Task WaitForCancel(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested) await Task.Delay(100, ct);
        }

        // ===== Monitor que dispara o cliente se não houver conexões =====
        static async Task StartClientMonitorAsync(ConcurrentDictionary<TcpClient, NetworkStream> clients, CancellationToken ct)
        {
            DateTime lastLaunch = DateTime.MinValue;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    bool noClients = clients.IsEmpty;

                    if (noClients && DateTime.UtcNow - lastLaunch >= LaunchCooldown)
                    {
                        if (!IsClientProcessRunning())
                        {
                            if (File.Exists(CLIENT_EXE_PATH))
                            {
                                Console.WriteLine($"[SERVER] Nenhum cliente conectado. Iniciando cliente: {CLIENT_EXE_PATH}");
                                var psi = new ProcessStartInfo
                                {
                                    FileName = CLIENT_EXE_PATH,
                                    UseShellExecute = false,
                                    CreateNoWindow = false,   // deixe true se quiser oculto
                                    WorkingDirectory = Path.GetDirectoryName(CLIENT_EXE_PATH)
                                };
                                try
                                {
                                    Process.Start(psi);
                                    lastLaunch = DateTime.UtcNow;
                                }
                                catch (Exception ex)
                                {
                                    System.IO.File.AppendAllText(logPathLog,
                                    DateTime.Now + $" [SERVER] Falha ao iniciar cliente: {ex.Message}\n");
                                }
                            }
                            else
                            {                                
                                System.IO.File.AppendAllText(logPathLog,
                                    DateTime.Now + $" [SERVER] CLIENT_EXE_PATH não encontrado: {CLIENT_EXE_PATH}\n");

                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" StartClientMonitorAsync->Erro: {ex.Message}\n");
                }

                await Task.Delay(2000, ct); // verifica a cada 2s
            }
        }

        static bool IsClientProcessRunning()
        {
            try
            {
                // Se o exe chama "LoopbackClient.exe", o nome do processo é "LoopbackClient"
                var name = Path.GetFileNameWithoutExtension(CLIENT_EXE_PATH);
                return Process.GetProcessesByName(name).Length > 0;
            }
            catch { return false; }
        }

    }
}
