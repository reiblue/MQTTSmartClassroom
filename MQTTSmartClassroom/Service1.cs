using LibreHardwareMonitor.Hardware;
using MQTTnet.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using static MQTTSmartClassroom.JsonStruture;

namespace MQTTSmartClassroom
{
    public partial class smartclassroom : ServiceBase
    {
        private Thread workerThread;
        private bool isRunning;
        int sleepTimeRunning = 60 * 1000 * 2;
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

        private string username = "service.windows.pc";
        private string password = "pc!C3PF#c102$2026@S3rv1c3.";

        /// <summary>
        /// Tentativas de iniciar o programa para manter a máquina acordada
        /// caso as tentativas se somem 30 desliga o computador
        /// </summary>
        private static byte tryToStartProgramKeepAwake = 0;
        private bool isConnectMqtt = false;

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
                    username: this.username,
                    password: this.password,
                    path + nameCertificate,
                    Environment.MachineName + "_" + "subscriber",
                    true
                    );

                await subscriber.ConnectAsync();
                await subscriber.SubscribeAsync(smartClassroomName + @"/" + Environment.MachineName);

                //PrependLogLine("OnStart->Subscriber", "Incrito topico: " + smartClassroomName + @"/" + Environment.MachineName);

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
                        username: this.username,
                        password: this.password,
                        Environment.MachineName + "_" + "publisher",
                        true,
                path + nameCertificate);

                broker.ConnectAsync().Wait();


                sleepTimeRunning = 1000 * 60 * timeMinutes; // Convertendo minutos para segundos

                await ConnectionTCP();
                PrependLogLine("OnStart->Subscriber->Action", "MQTT Publisher Ok!");

            }
            catch (Exception ex)
            {

                System.IO.File.AppendAllText(logPathLog,
                        DateTime.Now + $" OnStart->Erro: {ex.Message}\n");

            }

           
            
        }

        private readonly Dictionary<int, TimeSpan> _lastCpuTimes = new Dictionary<int, TimeSpan>();
        private DateTime _lastSampleUtc = DateTime.MinValue;


        private async void Executar()
        {
            while (isRunning)
            {
                try
                {


                    DateTime currentTime = DateTime.Now;

                    if (broker.isConnected)
                    {

                        try
                        {
                            var d = new DriveInfo("C");
                            long total = d.TotalSize;          // bytes
                            long free = d.TotalFreeSpace;     // bytes
                            long used = total - free;
                            double usedPct = total > 0 ? (used * 100.0 / total) : 0;

                            var diskInfo = new DiskStatus
                            {
                                COMPUTER = Environment.MachineName,
                                totalSize = total,
                                freeSpace = free,
                                usedSpace = used,
                                usedPercentage = usedPct
                            };

                            string payload = JsonSerializer.Serialize(diskInfo);

                            await broker.PublishAsync(smartClassroomName + @"/DISK_STATUS", payload.ToString());
                        }
                        catch (Exception ex)
                        {
                            PrependLogLine("ERRO-FIND-DISK", ex.Message);
                        }

                        try
                        {
                            var action = new ActionComputer
                            {
                                COMPUTER_NAME = Environment.MachineName,
                                ACTION = "KEETALIVE",
                                TIMESTAMP = DateTime.Now
                            };

                            string payload = JsonSerializer.Serialize(action);
                            //SHUTDOWN_COMPUTER
                            await broker.PublishAsync(smartClassroomName + @"/SHUTDOWN_COMPUTER", payload.ToString());
                        }
                        catch (Exception ex)
                        {
                            PrependLogLine("Executar->Erro", ex.Message);
                        }

                        Computer computer = new Computer
                        {
                            IsCpuEnabled = true,
                            IsGpuEnabled = true,
                            IsMemoryEnabled = true,
                            IsMotherboardEnabled = true,
                            IsStorageEnabled = true,
                            IsControllerEnabled = true,
                            IsPsuEnabled = true
                        };

                        computer.Open();

                        var hardwareList = new List<HardwareInfo>();

                        try
                        {
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

                        }
                        catch (Exception)
                        {
                            PrependLogLine("HardwareSensorsError", "Erro ao coletar sensores de hardware.");
                        }
                        finally
                        {
                            computer.Close();
                        }

                        
                        
                        // Converter para JSON
                        string json = JsonSerializer.Serialize(hardwareList, new JsonSerializerOptions { WriteIndented = true });

                        await broker.PublishAsync(smartClassroomName + @"/HARDWARE_SENSORS", json);
                        PrependLogLine("HardwareSensorsPublished", "Sensores de hardware publicados com sucesso.");

                        // ------------------- CPU por processo (delta entre iterações) -------------------
                        var nowUtc = DateTime.UtcNow;

                        // Primeira iteração: só “semeia” e sai
                        if (_lastSampleUtc == DateTime.MinValue)
                        {
                            _lastSampleUtc = nowUtc;

                            foreach (var p in Process.GetProcesses())
                            {
                                try
                                {
                                    p.Refresh();
                                    _lastCpuTimes[p.Id] = p.TotalProcessorTime;
                                }
                                catch { }
                                finally { p.Dispose(); }
                            }

                            // não tem delta ainda -> não publica CPU nesta rodada
                        }
                        else
                        {
                            double elapsedMs = (nowUtc - _lastSampleUtc).TotalMilliseconds;
                            if (elapsedMs < 1) elapsedMs = 1;

                            int cpuCores = Environment.ProcessorCount;
                            var listaProcessos = new List<ProcessInfo>();

                            foreach (var p in Process.GetProcesses())
                            {
                                try
                                {
                                    p.Refresh();
                                    if (p.HasExited) continue;

                                    var newCpu = p.TotalProcessorTime;

                                    if (!_lastCpuTimes.TryGetValue(p.Id, out var oldCpu))
                                    {
                                        // Processo novo desde a última amostra
                                        _lastCpuTimes[p.Id] = newCpu;
                                        continue;
                                    }

                                    double cpuUsedMs = (newCpu - oldCpu).TotalMilliseconds;
                                    if (cpuUsedMs < 0) cpuUsedMs = 0; // segurança

                                    double cpuPercent = (cpuUsedMs / elapsedMs) * 100.0 / cpuCores;

                                    // Evite arredondar cedo demais (isso mascara valores baixos)
                                    var info = ProcessInfo.MapProcessToInfo(p, cpuPercent, currentTime);
                                    listaProcessos.Add(info);

                                    _lastCpuTimes[p.Id] = newCpu;
                                }
                                catch
                                {
                                    // processos sem permissão / que morrem no meio do caminho
                                }
                                finally
                                {
                                    p.Dispose();
                                }
                            }

                            // Remove PIDs que sumiram (pra não crescer infinito)
                            var alive = Process.GetProcesses().Select(pr => pr.Id).ToHashSet();
                            foreach (var pid in _lastCpuTimes.Keys.Where(pid => !alive.Contains(pid)).ToList())
                                _lastCpuTimes.Remove(pid);

                            _lastSampleUtc = nowUtc;

                            var listaOrdenada = listaProcessos
                                .OrderByDescending(x => x.CpuPercentage)
                                .ToList();

                            var computerInfo = new ComputerInfo
                            {
                                ComputerName = Environment.MachineName,
                                ProcessList = listaOrdenada,
                                Timestamp = currentTime
                            };

                            json = JsonSerializer.Serialize(computerInfo, new JsonSerializerOptions { WriteIndented = true });
                            await broker.PublishAsync(smartClassroomName + @"/PROCESS_COMPUTERS", json);
                            PrependLogLine("ProcessListPublished", $"Lista de processos publicada com {listaOrdenada.Count} processos.");
                        }
                        // -------------------------------------------------------------------------------


                    }//Conectado ao MQTT

                    else
                    {

                        //espera por conexão automática
                        //caso exceda o tempo esperado de 5 tentativas tenta conectar manualmente
                        byte tryConnect = 0;

                        //while (!broker.isConnected)
                        {
                            //tryConnect += 1;

                            //if (tryConnect > 5)
                            {
                                try
                                {
                                    await broker.ConnectAsync();
                                    broker.isConnected = true;
                                }
                                catch (Exception ex)
                                {
                                    PrependLogLine("Running->ConnectMqtt->SubReconnect->Erro", ex.Message);
                                }
                                
                            }
                            //Thread.Sleep(TimeSpan.FromMinutes(2));
                        }
                    }

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
                                                if (!processOff.Contains(command.name))
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
                            foreach (var action in actionTCPLoopback)
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


                    if (tryToStartProgramKeepAwake > 45)
                    {
                        PrependLogLine("Shutdown", "Número máximo de tentativas de iniciar o programa para manter a máquina acordada atingido. Desligando o computador.");
                        Process.Start("shutdown", "/s /t 0 /F");
                    }


                }
                catch (MqttCommunicationException ex)
                {
                    PrependLogLine("Running->MqttCommunicationException->Erro", "Falha na comunicação MQTT: " + ex.Message);
                    //await broker.DisconnectAsync();   
                    //await subscriber.DisconnectAsync();
                    //isConnectMqtt = false;
                }
                catch (Exception ex)
                {
                    //System.IO.File.AppendAllText(logPathLog,
                    //    DateTime.Now +  $" Running->Erro: {ex.Message}\n");
                    PrependLogLine("Running->Erro", ex.Message);
                }

                finally
                {
                    Thread.Sleep(sleepTimeRunning);
                    //await broker.DisconnectAsync();
                }


            }
        }

        protected async override void OnStop()
        {

            try
            {
                //broker.ConnectAsync();
                var action = new ActionComputer
                {
                    COMPUTER_NAME = Environment.MachineName,
                    ACTION = "SHUTDOWN",
                    TIMESTAMP = DateTime.Now
                };


                string json = JsonSerializer.Serialize(action);
                broker.PublishAsync(smartClassroomName + @"/SHUTDOWN_COMPUTER", json.ToString());
            }
            catch (Exception ex)
            {
                PrependLogLine("OnShutdown->Erro", ex.Message);
            }
            

            isRunning = false;
            await broker.DisconnectAsync();
            if (workerThread != null && workerThread.IsAlive)
                workerThread.Join();  
            
        }

        protected override void OnShutdown()
        {

            try
            {
                //broker.ConnectAsync();
                var action = new ActionComputer
                {
                    COMPUTER_NAME = Environment.MachineName,
                    ACTION = "SHUTDOWN",
                    TIMESTAMP = DateTime.Now
                };


                while (!broker.isConnected)
                {
                    try
                    {
                        broker.ConnectAsync().Wait();
                    }
                    catch (Exception ex)
                    {
                        PrependLogLine("OnShutdown->ConnectMqtt->Erro", ex.Message);
                    }
                }

                string json = JsonSerializer.Serialize(action);
                broker.PublishAsync(smartClassroomName + @"/SHUTDOWN_COMPUTER", json.ToString()).Wait();
                PrependLogLine("OnShutdown", "Mensagem de shutdown enviada ao broker MQTT.");
            }
            catch (Exception ex)
            {
                PrependLogLine("OnShutdown->Erro", ex.Message);
            }


            isRunning = false;
            broker.DisconnectAsync().Wait();
            if (workerThread != null && workerThread.IsAlive)
                workerThread.Join();

            PrependLogLine("OnShutdown", "Serviço finalizado durante o shutdown do Windows.");

            // código executado especificamente durante o shutdown do Windows
            base.OnShutdown();

            

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
                                        tryToStartProgramKeepAwake += 1;
                                        Thread.Sleep(1000 * 60);
                                }

                                }
                                else
                                {
                                    System.IO.File.AppendAllText(logPathLog,
                                        DateTime.Now + $" [SERVER] CLIENT_EXE_PATH não encontrado: {CLIENT_EXE_PATH}\n");
                                    tryToStartProgramKeepAwake += 1;
                                    Thread.Sleep(1000 * 60);

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
