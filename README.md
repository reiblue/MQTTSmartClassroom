🌐 **Português** | [English](./README.en.md)

# MQTTSmartClassroom — Agente de Telemetria dos Computadores (Windows Service)

Serviço do Windows (**C# / .NET Framework 4.8**) instalado em cada computador dos laboratórios inteligentes (**Smart Lab / Smart Classroom**) do **Instituto Federal do Rio de Janeiro (IFRJ) — Campus Eng. Paulo de Frontin**.

O serviço (**SmartClassroom**) roda em segundo plano em cada estação de trabalho, coletando **telemetria de hardware** (temperatura de CPU, sensores, I/O e status de disco, processos, ociosidade) e publicando os dados via **MQTT com TLS** para o broker central do laboratório. Ele também **recebe comandos remotos** (ex.: desligamento) e gerencia o utilitário `SmartLabKeepToAwake` na sessão do usuário.

> Este projeto é o **agente cliente** do ecossistema Smart Lab. Os serviços de backend estão em [ServicosLinux](https://github.com/reiblue/ServicosLinux) e o painel do gêmeo digital em [DigitalTwinSmartLab](https://github.com/reiblue/DigitalTwinSmartLab).

> **Autor:** Rodrigo Mendes Peixoto — rodrigo.peixoto@ifrj.edu.br

---

## 🏗️ Papel no Ecossistema

```
[PC do laboratório]                         [Servidor Smart Lab]
+---------------------------+
| SmartClassroom (serviço)  | --telemetria--> [Broker MQTT/TLS] --> [Listeners Python]
|  - Sensores de hardware   | <--comandos---        |                     |
|  - Processos / ociosidade |                       v                     v
|  - Disco (I/O e status)   |            [DigitalTwinSmartLab]      [PostgreSQL]
+------------+--------------+               (painel em tempo real)
             | TCP loopback (127.0.0.1:9777)
             v
| SmartLabKeepToAwake.exe (sessão do usuário) |
```

---

## ⚙️ Funcionalidades

- **Telemetria de hardware** — leitura de sensores via **LibreHardwareMonitor** e **OpenHardwareMonitor** (temperatura da CPU, clocks etc.), publicada em JSON no broker.
- **Monitoramento de disco** — I/O em tempo real (`DiskIoMonitor`) e status geral dos discos (`DiskStatus`).
- **Monitoramento de processos** — inventário dos processos em execução na máquina (`ProcessInfo` / `ProcessSetInfo`).
- **Detecção de ociosidade** — mede o tempo ocioso da estação (`IdleTime`); se a máquina ficar inativa além do limite configurado, o serviço pode **desligá-la automaticamente** para economizar energia.
- **Comandos remotos** — assina tópicos MQTT e executa ações recebidas (`ActionComputer`), como desligamento remoto do computador.
- **Integração com a sessão do usuário** — inicia o `SmartLabKeepToAwake.exe` na sessão ativa (`UserSessionLauncher`) e se comunica com ele por um **servidor TCP de loopback** (`LoopbackServer`, porta 9777). Se o utilitário falhar repetidas vezes (30 tentativas), o computador é desligado.
- **Atualização automática** — o serviço é atualizado de forma centralizada pelo script `UpdateServiceWindows.ps1` (no repositório [ServicosLinux](https://github.com/reiblue/ServicosLinux)), que compara o `Version.txt` local com o do deploy central.

---

## 📂 Estrutura do Projeto

| Arquivo | Descrição |
|---|---|
| `Program.cs` | Ponto de entrada — registra e executa o serviço do Windows (`smartclassroom`). |
| `Service1.cs` | Núcleo do serviço: laço de coleta de telemetria, conexão MQTT, tratamento de comandos, controle de ociosidade e do keep-awake. |
| `MqttPublisher.cs` / `MqttSubscriber.cs` | Comunicação MQTT (MQTTnet) com TLS — publicação da telemetria e assinatura de comandos. |
| `CpuTemperatureMonitor.cs` | Leitura da temperatura da CPU e sensores de hardware. |
| `DiskIoMonitor.cs` / `DiskStatus.cs` | Monitoramento de I/O e status dos discos (WMI / contadores de desempenho). |
| `ProcessInfo.cs` / `ProcessSetInfo.cs` | Coleta de informações dos processos em execução. |
| `IdleTime.cs` | Medição do tempo de ociosidade da estação. |
| `ActionComputer.cs` | Modelo dos comandos recebidos via MQTT (computador, ação, timestamp). |
| `UserSessionLauncher.cs` | Inicia processos na sessão interativa do usuário a partir do serviço (WTS API). |
| `LoopbackServer.cs` | Servidor TCP local (127.0.0.1:9777) para comunicação com o `SmartLabKeepToAwake`. |
| `JsonStruture.cs` / `ComputerInfo.cs` | Estruturas JSON dos payloads publicados (hardware, sensores, timestamps). |

### Arquivos de configuração (na pasta do serviço)

O serviço lê a configuração de arquivos `.txt` simples em `C:\Program Files\SmartClassroom\`:

| Arquivo | Exemplo | Função |
|---|---|---|
| `IPBroker.txt` | `10.11.102.123` | Endereço do broker MQTT. |
| `SmartClassroomName.txt` | `C102` | Nome do laboratório (prefixo dos tópicos). |
| `NameCertificate.txt` | `ca.crt` | Nome do certificado TLS. |
| `TimerMinutes.txt` | `1` | Intervalo (min) do ciclo de coleta/publicação. |
| `IdleMaxMinutes.txt` | `40` | Limite de ociosidade antes do desligamento automático. |
| `IsStopSmartLabKeepAwakeRunning.txt` | `true` | Controla a execução do keep-awake. |
| `Version.txt` | `1.0.9` | Versão instalada (usada pelo atualizador central). |

---

## 🚀 Instalação (visão geral)

### Pré-requisitos

- **Windows** com **.NET Framework 4.8**
- Certificado `ca.crt` do broker MQTT do laboratório
- Porta **8883** liberada no firewall (ver scripts em [ServicosLinux](https://github.com/reiblue/ServicosLinux))

### Passos

1. Compile a solução `MQTTSmartClassroom.sln` (Visual Studio, `Release`).
2. Copie os binários e os arquivos `.txt` de configuração para `C:\Program Files\SmartClassroom\` (incluindo o `SmartLabKeepToAwake.exe` e o `ca.crt`).
3. Ajuste os arquivos de configuração (`IPBroker.txt`, `SmartClassroomName.txt` etc.).
4. Instale o serviço do Windows, por exemplo:
   ```powershell
   sc.exe create SmartClassroom binPath= "C:\Program Files\SmartClassroom\MQTTSmartClassroom.exe" start= auto
   sc.exe start SmartClassroom
   ```
5. Para atualizações centralizadas, use o `UpdateServiceWindows.ps1` do repositório ServicosLinux.

---

## 📡 Dados publicados (exemplos de tópicos)

Com `SmartClassroomName.txt = C102`, o serviço publica/consome tópicos como:

| Tópico | Conteúdo |
|---|---|
| `C102/PROCESS_COMPUTERS` | Processos e atividade dos computadores |
| `C102/DISK_STATUS` | Status e I/O dos discos |
| `C102/IDLE` | Tempo de ociosidade das estações |
| `C102/SHUTDOWN_COMPUTER` | Comandos de desligamento remoto |
| `C102/LAST_STATUS` / `C102/STATUS` | Status geral das máquinas |

Payloads de exemplo estão em [`ServicosLinux/jsonExamples`](https://github.com/reiblue/ServicosLinux/tree/main/jsonExamples).

---

## 🛠️ Tecnologias

- **C# / .NET Framework 4.8** — Windows Service
- **MQTTnet** — comunicação MQTT com TLS
- **LibreHardwareMonitor / OpenHardwareMonitor** — sensores de hardware
- **System.Management (WMI)** — informações do sistema
- **System.Text.Json** — serialização dos payloads

---

## 🔗 Projetos relacionados

- [ServicosLinux](https://github.com/reiblue/ServicosLinux) — backend do Smart Lab (broker Mosquitto, listeners Python, PostgreSQL, scripts de infraestrutura e o atualizador `UpdateServiceWindows.ps1`).
- [DigitalTwinSmartLab](https://github.com/reiblue/DigitalTwinSmartLab) — painel do gêmeo digital, simulação e ML (inclui o projeto `SmartLabKeepToAwake`).

---

## 📄 Licença

Projeto acadêmico/institucional — IFRJ Campus Eng. Paulo de Frontin. Defina aqui a licença desejada (ex.: MIT, GPL-3.0).
