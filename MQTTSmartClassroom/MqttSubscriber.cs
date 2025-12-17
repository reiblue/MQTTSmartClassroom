using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTSmartClassroom;
using System;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;

namespace MQTTClient
{
    public class MqttSubscriber
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _mqttOptions;
        public List<string> payloads;
        public bool isConnected = false;

        public MqttSubscriber(string brokerIp, int brokerPort, string username, string password, string pathCertificate,string idClient,  bool useTls = false)
        {
            
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            this.payloads = new List<string>();

            X509Certificate2 caCert = new X509Certificate2(pathCertificate);

            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerIp, brokerPort)
                .WithClientId(idClient)
                .WithCleanSession();

            
            

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                builder.WithCredentials(username, password);
            }

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

            _mqttOptions = builder.Build();

            // Eventos
            _mqttClient.ApplicationMessageReceivedAsync += HandleReceivedMessage;
            _mqttClient.ConnectedAsync += e =>
            {
                Console.WriteLine("✅ Conectado ao broker MQTT.");
                return Task.CompletedTask;
            };
            _mqttClient.DisconnectedAsync += e =>
            {
                Console.WriteLine("⚠️ Desconectado do broker MQTT.");
                return Task.CompletedTask;
            };
        }

        // Conectar ao broker
        public async Task ConnectAsync()
        {
            await _mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);
        }

        // Desconectar
        public async Task DisconnectAsync()
        {
            await _mqttClient.DisconnectAsync();
        }

        // Inscrever-se em um tópico
        public async Task SubscribeAsync(string topic)
        {

            try
            {

                if (!_mqttClient.IsConnected)
                {
                    Console.WriteLine("⚠️ Não conectado. Tentando conectar...");
                    await ConnectAsync();
                }

                await _mqttClient.SubscribeAsync(topic);
                smartclassroom.PrependLogLine("MQTT", "Inscrito no tópico: " + topic);
            }
            catch (TimeoutException ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Tempo esgotado ao receber mensagem: " + ex.Message);
            }
            catch (MqttCommunicationException ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Erro de comunicação MQTT: " + ex.Message);
            }
            catch (Exception ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Erro ao processar mensagem MQTT: " + ex.Message);
            }

        }

        public async Task KeepConnection()
        {
            try
            {
                if (!_mqttClient.IsConnected)
                {
                    await ConnectAsync();
                    this.isConnected = true;
                }

            }
            catch (TimeoutException ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Tempo esgotado ao receber mensagem: " + ex.Message);
            }
            catch (MqttCommunicationException ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Erro de comunicação MQTT: " + ex.Message);
            }
            catch (Exception ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Erro ao processar mensagem MQTT: " + ex.Message);
            }
        }

        // Manipular mensagens recebidas
        private Task HandleReceivedMessage(MqttApplicationMessageReceivedEventArgs e)
        { 

            try
            {
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                //Console.WriteLine("📨 Mensagem recebida:");
                //Console.WriteLine($"→ Tópico: {e.ApplicationMessage.Topic}");
                //Console.WriteLine($"→ Payload: {payload}");
                //Console.WriteLine("-----------------------------");
                UpdatePayload(payload);
            }
            catch (TimeoutException ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Tempo esgotado ao receber mensagem: " + ex.Message);
            }
            catch (MqttCommunicationException ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Erro de comunicação MQTT: " + ex.Message);
            }
            catch (Exception ex)
            {
                smartclassroom.PrependLogLine("ERRO MQTT", "Erro ao processar mensagem MQTT: " + ex.Message);
            }
            
            return Task.CompletedTask;
            

        }

      

        public void UpdatePayload(string payload)
        {
            this.payloads.Add(payload);
        }
    }
}
