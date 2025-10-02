using MQTTnet.Client;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MQTTSmartClassroom
{
    public class LoopbackServer
    {
        private readonly int port;
        private TcpListener listener;
        private bool isRunning = true;

        public LoopbackServer(int port = 9777) { this.port = port; }

        public void Start(Func<TcpClient, Task> handler)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Task.Run(async () =>
            {
                while (isRunning)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = handler(client);
                }
            });
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                var msg = Encoding.UTF8.GetString(buffer, 0, read);
                Console.WriteLine("Recebido: " + msg);
                // opcional: responder
                var response = Encoding.UTF8.GetBytes("OK");
                await stream.WriteAsync(response, 0, response.Length);
            }
        }

        public void Stop() => listener?.Stop();
    }
}
