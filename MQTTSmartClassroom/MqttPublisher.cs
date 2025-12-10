using MQTTnet;
using MQTTnet.Client;
using MQTTSmartClassroom;
using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;

namespace MQTTClient
{
    public class MqttPublisher
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _options;

        public MqttPublisher(string brokerAddress, string idClient, int brokerPort = 1883)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerAddress, brokerPort)
                .WithCleanSession()  
                .WithClientId(idClient)
                .Build();

        }

        public MqttPublisher(string brokerAddress, int brokerPort, string username, string password, string idClient, bool useTls = false,  string pathCertificate = null)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            X509Certificate2 caCert = new X509Certificate2(pathCertificate);

            MqttClientOptionsBuilder builder = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerAddress, brokerPort)
                .WithClientId(idClient)
                .WithCleanSession();

            // 🔐 Usuário e senha
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                builder.WithCredentials(username, password);
            }

            // 🔒 TLS/SSL
            if (useTls)
            {
                builder.WithTls(tls =>
                {
                    tls.UseTls = true; // 🔐 Ativa TLS/SSL
                    tls.Certificates = new List<X509Certificate2> { caCert };
                    tls.AllowUntrustedCertificates = true;
                    tls.IgnoreCertificateChainErrors = true;
                    tls.IgnoreCertificateRevocationErrors = true;
                    tls.CertificateValidationHandler = _ => true;

                    // 🔐 Em produção, configure validação real, exemplo:
                    // tls.CertificateValidationHandler = context =>
                    // {
                    //     return context.Certificate.Subject.Contains("CN=broker.domain.com");
                    // };
                    // ➕ (Opcional) Forçar protocolo TLS específico:
                    // tls.SslProtocol = System.Security.Authentication.SslProtocols.Tls12;
                });
            }

           

            _options = builder.Build();

            _mqttClient.DisconnectedAsync += async e =>
            {
                // Log para debug (pode trocar por gravação em arquivo/banco)
                smartclassroom.PrependLogLine($"[MQTT] Desconectado. Motivo: {e.Reason}", "Desconexao");

                // Se quiser tentar reconectar automaticamente para sempre:
                await Task.Delay(TimeSpan.FromSeconds(5)); // Espera 5s antes de tentar

                try
                {
                    // Tenta reconectar usando as opções já configuradas (_options)
                    // Importante: Como estamos dentro de um evento, o async/await funciona bem aqui
                    await _mqttClient.ConnectAsync(_options, CancellationToken.None);
                    Console.WriteLine("[MQTT] Reconectado com sucesso!");
                    smartclassroom.PrependLogLine("[MQTT] Reconectado com sucesso!", "Reconexao");
                }
                catch(Exception ex)
                {
                    smartclassroom.PrependLogLine("[MQTT] Falha ao tentar reconectar.", ex.Message);
                }
            };
        }


        public async Task ConnectAsync()
        {
            try
            {
                await _mqttClient.ConnectAsync(_options);
                Console.WriteLine("Conectado ao broker MQTT!");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }



        public async Task PublishAsync(string topic, string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce) // QoS 1
                .WithRetainFlag(false)
                .Build();

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.PublishAsync(message);
                Console.WriteLine($"Mensagem publicada no tópico '{topic}': {payload}");
            }
            else
            {
                Console.WriteLine("Não está conectado ao broker!");
            }
        }

        public async Task DisconnectAsync()
        {
            await _mqttClient.DisconnectAsync();
            Console.WriteLine("Desconectado do broker.");
        }

        
    }
}
