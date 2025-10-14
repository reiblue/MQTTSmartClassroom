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
using System.Management;
using System.Text.Json.Nodes;

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
        private static int maxLines = 99;

        private MQTTClient.MqttSubscriber subscriber;

        private List<string> processOff = new List<string>();
        private static List<string> actionTCPLoopback = new List<string>();

        public smartclassroom()
        {
            InitializeComponent();

            //try
            //{
            //    //Limpar o arquivo de texto 
            //    System.IO.File.WriteAllText(logPathLog, string.Empty);
            //}
            //catch (Exception ex)
            //{
            //    PrependLogLine("ConstructoMethod->CleanText->Erro", ex.Message);
            //}

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
                //Limpar o arquivo de texto 
                System.IO.File.WriteAllText(logPathLog, string.Empty);
            }
            catch (Exception ex)
            {
                PrependLogLine("ConstructoMethod->CleanText->Erro", ex.Message);
            }

            try
            {
                PrependLogLine("OnStart->Subscriber->Action", "Subscriber acionado");

                subscriber = new MQTTClient.MqttSubscriber(
                    IPBroker,
                    port,
                    null,
                    null,
                    path + nameCertificate,
                    true);

                await subscriber.ConnectAsync();
                await subscriber.SubscribeAsync(smartClassroomName + @"/" + Environment.MachineName);

                PrependLogLine("OnStart->Subscriber", "Incrito topico: " + smartClassroomName + @"/" + Environment.MachineName);

            }
            catch (Exception ex)
            {
                PrependLogLine("OnStart->Subscriber->Erro", ex.Message);
            }

            try
            {
                PrependLogLine("OnStart->Subscriber->Action", "Iniciando MQTT Publisher");
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
                PrependLogLine("OnStart->Subscriber->Action", "MQTT Publisher Ok!");

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

                    //Verifica se existe lista de payload
                    

                    try
                    {
                        if (subscriber.payloads != null)
                            if (subscriber.payloads.Count > 0)
                            {
                                foreach (var payload in subscriber.payloads)
                                {
                                    try
                                    {

                                        var command = JsonSerializer.Deserialize<ProcessSetInfo>(payload);
                                        if (command != null)
                                        {

                                            if (command.action == "include")
                                            {
                                                if(!processOff.Contains(command.name))
                                                {
                                                    processOff.Add(command.name);
                                                    PrependLogLine("ProcessAdded", $"Processo {command.name} adicionado à lista de encerramento.");
                                                }
                                                else
                                                {
                                                    PrependLogLine("ProcessAddError", $"Processo {command.name} já existe na lista de encerramento.");
                                                }
                                            }
                                            else if (command.action == "remove")
                                            {
                                                processOff.RemoveAt(processOff.IndexOf(command.name));
                                                PrependLogLine("ProcessRemoved", $"Processo {command.name} removido da lista de encerramento.");
                                            }
                                        }
                                    }


                                    catch (Exception ex)
                                    {
                                        PrependLogLine("Deserialize->Erro", ex.Message);
                                    }
                                }
                                // Limpa a lista de payloads após o processamento
                                subscriber.payloads.Clear();
                            }

                        if (processOff.Count > 0)
                        {
                            foreach (var processName in processOff)
                            {


                                var processosParaFechar = Process.GetProcessesByName(processName);
                                if (processosParaFechar.Count() > 0)
                                    foreach (var proc in processosParaFechar)
                                    {
                                        try
                                        {
                                            proc.Kill();
                                            PrependLogLine("ProcessKilled", $"Processo {processName} (PID: {proc.Id}) finalizado.");
                                        }
                                        catch (Exception ex)
                                        {
                                            PrependLogLine("ProcessKillError", $"Erro ao finalizar processo {processName} (PID: {proc.Id}): {ex.Message}");
                                        }
                                    }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PrependLogLine("Running->Process->Erro", ex.Message);
                    }

                    await subscriber.KeepConnection();

                    try
                    {
                        if (actionTCPLoopback.Count > 0)
                        {
                            foreach(var action in actionTCPLoopback)
                            {
                                var obj = JsonNode.Parse(action).AsObject();
                                obj["name"] = Environment.MachineName;
                                await broker.PublishAsync(smartClassroomName + @"/IDLE", obj.ToJsonString());
                            }
                            actionTCPLoopback.Clear();
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        PrependLogLine("Running->Process->Erro", ex.Message);
                    }


                }
                catch (Exception ex)
                {
                    //System.IO.File.AppendAllText(logPathLog,
                    //    DateTime.Now +  $" Running->Erro: {ex.Message}\n");
                    PrependLogLine("Running->Erro", ex.Message);
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
                                   DateTime.Now + $" [SERVER] Escutando em {BindAddress}:{Port} \n");

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

                        PrependLogLine("RECV", line);

                        actionTCPLoopback.Add(line);

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
                PrependLogLine("Exception-HandleClientAsync", ex.Message);
            }
            finally
            {
                clients.TryRemove(client, out _);
                try { stream.Close(); client.Close(); } catch { }
                //System.IO.File.AppendAllText(logPathLog,
                //                   DateTime.Now + $" [SERVER] Cliente desconectado.\n");
                PrependLogLine("finally-HandleClientAsync", "Cliente desconectado.");
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
        /// <summary>
        /// Inicio o serviço monitorando se há clientes conectados.
        /// O serviço escuta em loopback e aceita apenas 1 cliente.
        /// Caso ja não haja clientes conectados, tenta iniciar o cliente e
        /// caso já esteja rodando um cliente envia comando para fechar.
        /// </summary>
        /// <param name="clients"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        static async Task StartClientMonitorAsync(ConcurrentDictionary<TcpClient, NetworkStream> clients, CancellationToken ct)
        {
            DateTime lastLaunch = DateTime.MinValue;

            // espera 5 minutos antes de começar a monitorar
            //await Task.Delay(1000 * 60 * 5, ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    bool noClients = clients.IsEmpty;

                    if (noClients && DateTime.UtcNow - lastLaunch >= LaunchCooldown)
                    {
                        if (!IsClientProcessRunning())
                        {
                            //if (UsuarioUsaLoginLocalDaMaquina())
                                if (File.Exists(CLIENT_EXE_PATH))
                                {
                                    PrependLogLine("SERVER", $"Nenhum cliente conectado. Iniciando cliente: {CLIENT_EXE_PATH}");
                                    //var psi = new ProcessStartInfo
                                    //{
                                    //    FileName = CLIENT_EXE_PATH,
                                    //    UseShellExecute = false,
                                    //    CreateNoWindow = false,   // deixe true se quiser oculto
                                    //    WorkingDirectory = Path.GetDirectoryName(CLIENT_EXE_PATH)
                                    //};
                                    //try
                                    //{
                                    //    Process.Start(psi);
                                    //    lastLaunch = DateTime.UtcNow;
                                    //}
                                    //catch (Exception ex)
                                    //{
                                    //    System.IO.File.AppendAllText(logPathLog,
                                    //    DateTime.Now + $" [SERVER] Falha ao iniciar cliente: {ex.Message}\n");
                                    //}

                                    try
                                    {
                                        var proc = UserSessionLauncher.StartInActiveUserSession(CLIENT_EXE_PATH, "--arg1");
                                        //bool visivel = VisibilityChecks.WaitUntilVisible(proc, TimeSpan.FromSeconds(10));

                                        //// Logue o resultado
                                        //if (visivel)
                                        //    EventLog.WriteEntry("MeuServico", "Aplicativo iniciado e visível ao usuário.", EventLogEntryType.Information);
                                        //else
                                        //    EventLog.WriteEntry("MeuServico", "Aplicativo iniciado, mas não foi possível confirmar visibilidade.", EventLogEntryType.Warning);
                                    }
                                    catch (Exception ex)
                                    {
                                        EventLog.WriteEntry("MeuServico", $"Falha ao iniciar app para o usuário: {ex.Message}", EventLogEntryType.Error);
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

        public static void PrependLogLine(string option, string payload)
        {
            // monta a linha com timestamp, ajuste o formato se quiser
            string newLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{option}] {payload}";

            /*
            // lê até (maxLines-1) linhas existentes (para somar com a nova e dar maxLines)
            IEnumerable<string> tail =
                File.Exists(logPathLog)
                    ? File.ReadLines(logPathLog).Take(maxLines - 1)
                    : Enumerable.Empty<string>();

            // novo conteúdo: nova linha + primeiras (maxLines-1) já existentes
            var linesToWrite = new[] { newLine }.Concat(tail);

            // reescreve o arquivo todo
            File.WriteAllLines(logPathLog, linesToWrite);*/

            System.IO.File.AppendAllText(logPathLog, newLine + $"\n");
        }

        private static bool UsuarioUsaLoginLocalDaMaquina()
        {
            try
            {
                var s = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
                foreach (var o in s.Get())
                {
                    var full = o["UserName"] as string;         // ex: "MINHAMAQUINA\\rodrigo" ou "MEUDOMINIO\\rodrigo"
                    if (string.IsNullOrEmpty(full)) return false; // ninguém logado

                    var parts = full.Split('\\');
                    var domain = parts.Length > 1 ? parts[0] : "";
                    return string.Equals(domain, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                PrependLogLine("UsuarioUsaLoginLocalDaMaquina", ex.Message);
            }
            
            return false;
        }


        private static bool UsuarioUsaLoginLocalDaMaquina(bool action_2)
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect(); // garante conexão

                var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT UserName FROM Win32_ComputerSystem"));

                foreach (ManagementObject o in searcher.Get())
                {
                    var full = o["UserName"] as string; // e.g. MAQUINA\rodrigo ou DOMINIO\rodrigo
                    if (string.IsNullOrEmpty(full)) return false; // ninguém logado

                    var parts = full.Split('\\');
                    var domain = parts.Length > 1 ? parts[0] : "";
                    return string.Equals(domain, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (ManagementException mex)
            {
                // Logue para diagnosticar (mex.ErrorCode / mex.Message)
                PrependLogLine("WMI", $"WMI falhou: {mex.ErrorCode} - {mex.Message}");
            }
            catch (Exception ex)
            {
                PrependLogLine("ERR", ex.ToString());
            }
            return false;
        }


}
}
