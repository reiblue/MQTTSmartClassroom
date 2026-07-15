🌐 [Português](./README.md) | **English**

# MQTTSmartClassroom — Computer Telemetry Agent (Windows Service)

A Windows service (**C# / .NET Framework 4.8**) installed on every computer of the smart laboratories (**Smart Lab / Smart Classroom**) at the **Federal Institute of Rio de Janeiro (IFRJ) — Eng. Paulo de Frontin Campus**, Brazil.

The service (**SmartClassroom**) runs in the background on each workstation, collecting **hardware telemetry** (CPU temperature, sensors, disk I/O and status, processes, idle time) and publishing the data via **MQTT over TLS** to the laboratory's central broker. It also **receives remote commands** (e.g., shutdown) and manages the `SmartLabKeepToAwake` utility in the user session.

> This project is the **client agent** of the Smart Lab ecosystem. The backend services live in [ServicosLinux](https://github.com/reiblue/ServicosLinux) and the digital twin dashboard in [DigitalTwinSmartLab](https://github.com/reiblue/DigitalTwinSmartLab).

> **Author:** Rodrigo Mendes Peixoto 

---

## 🏗️ Role in the Ecosystem

```
[Lab PC]                                    [Smart Lab Server]
+---------------------------+
| SmartClassroom (service)  | --telemetry--> [MQTT/TLS Broker] --> [Python listeners]
|  - Hardware sensors       | <--commands---        |                     |
|  - Processes / idle time  |                       v                     v
|  - Disk (I/O and status)  |            [DigitalTwinSmartLab]      [PostgreSQL]
+------------+--------------+               (real-time dashboard)
             | TCP loopback (127.0.0.1:9777)
             v
| SmartLabKeepToAwake.exe (user session) |
```

---

## ⚙️ Features

- **Hardware telemetry** — sensor readings via **LibreHardwareMonitor** and **OpenHardwareMonitor** (CPU temperature, clocks, etc.), published as JSON to the broker.
- **Disk monitoring** — real-time I/O (`DiskIoMonitor`) and overall disk status (`DiskStatus`).
- **Process monitoring** — inventory of the processes running on the machine (`ProcessInfo` / `ProcessSetInfo`).
- **Idle detection** — measures the workstation's idle time (`IdleTime`); if the machine stays inactive beyond the configured threshold, the service can **shut it down automatically** to save energy.
- **Remote commands** — subscribes to MQTT topics and executes received actions (`ActionComputer`), such as remote computer shutdown.
- **User-session integration** — launches `SmartLabKeepToAwake.exe` in the active session (`UserSessionLauncher`) and communicates with it through a **TCP loopback server** (`LoopbackServer`, port 9777). If the utility fails repeatedly (30 attempts), the computer is shut down.
- **Automatic updates** — the service is centrally updated by the `UpdateServiceWindows.ps1` script (in the [ServicosLinux](https://github.com/reiblue/ServicosLinux) repository), which compares the local `Version.txt` against the central deploy.

---

## 📂 Project Structure

| File | Description |
|---|---|
| `Program.cs` | Entry point — registers and runs the Windows service (`smartclassroom`). |
| `Service1.cs` | Core of the service: telemetry collection loop, MQTT connection, command handling, idle-time and keep-awake control. |
| `MqttPublisher.cs` / `MqttSubscriber.cs` | MQTT communication (MQTTnet) with TLS — telemetry publishing and command subscription. |
| `CpuTemperatureMonitor.cs` | CPU temperature and hardware sensor readings. |
| `DiskIoMonitor.cs` / `DiskStatus.cs` | Disk I/O and status monitoring (WMI / performance counters). |
| `ProcessInfo.cs` / `ProcessSetInfo.cs` | Collection of running process information. |
| `IdleTime.cs` | Workstation idle-time measurement. |
| `ActionComputer.cs` | Model of the commands received via MQTT (computer, action, timestamp). |
| `UserSessionLauncher.cs` | Launches processes in the interactive user session from the service (WTS API). |
| `LoopbackServer.cs` | Local TCP server (127.0.0.1:9777) for communication with `SmartLabKeepToAwake`. |
| `JsonStruture.cs` / `ComputerInfo.cs` | JSON structures of the published payloads (hardware, sensors, timestamps). |

### Configuration files (in the service folder)

The service reads its configuration from simple `.txt` files in `C:\Program Files\SmartClassroom\`:

| File | Example | Purpose |
|---|---|---|
| `IPBroker.txt` | `10.11.102.123` | MQTT broker address. |
| `SmartClassroomName.txt` | `C102` | Laboratory name (topic prefix). |
| `NameCertificate.txt` | `ca.crt` | TLS certificate name. |
| `TimerMinutes.txt` | `1` | Interval (min) of the collection/publishing cycle. |
| `IdleMaxMinutes.txt` | `40` | Idle limit before automatic shutdown. |
| `IsStopSmartLabKeepAwakeRunning.txt` | `true` | Controls the keep-awake execution. |
| `Version.txt` | `1.0.9` | Installed version (used by the central updater). |

---

## 🚀 Installation (overview)

### Prerequisites

- **Windows** with **.NET Framework 4.8**
- The lab MQTT broker's `ca.crt` certificate
- Port **8883** open in the firewall (see the scripts in [ServicosLinux](https://github.com/reiblue/ServicosLinux))

### Steps

1. Build the `MQTTSmartClassroom.sln` solution (Visual Studio, `Release`).
2. Copy the binaries and the `.txt` configuration files to `C:\Program Files\SmartClassroom\` (including `SmartLabKeepToAwake.exe` and `ca.crt`).
3. Adjust the configuration files (`IPBroker.txt`, `SmartClassroomName.txt`, etc.).
4. Install the Windows service, for example:
   ```powershell
   sc.exe create SmartClassroom binPath= "C:\Program Files\SmartClassroom\MQTTSmartClassroom.exe" start= auto
   sc.exe start SmartClassroom
   ```
5. For centralized updates, use `UpdateServiceWindows.ps1` from the ServicosLinux repository.

---

## 📡 Published data (topic examples)

With `SmartClassroomName.txt = C102`, the service publishes/consumes topics such as:

| Topic | Content |
|---|---|
| `C102/PROCESS_COMPUTERS` | Computer processes and activity |
| `C102/DISK_STATUS` | Disk status and I/O |
| `C102/IDLE` | Workstation idle time |
| `C102/SHUTDOWN_COMPUTER` | Remote shutdown commands |
| `C102/LAST_STATUS` / `C102/STATUS` | Overall machine status |

Example payloads are available in [`ServicosLinux/jsonExamples`](https://github.com/reiblue/ServicosLinux/tree/main/jsonExamples).

---

## 🛠️ Technologies

- **C# / .NET Framework 4.8** — Windows Service
- **MQTTnet** — MQTT communication with TLS
- **LibreHardwareMonitor / OpenHardwareMonitor** — hardware sensors
- **System.Management (WMI)** — system information
- **System.Text.Json** — payload serialization

---

## 🔗 Related projects

- [ServicosLinux](https://github.com/reiblue/ServicosLinux) — Smart Lab backend (Mosquitto broker, Python listeners, PostgreSQL, infrastructure scripts, and the `UpdateServiceWindows.ps1` updater).
- [DigitalTwinSmartLab](https://github.com/reiblue/DigitalTwinSmartLab) — digital twin dashboard, simulation, and ML (includes the `SmartLabKeepToAwake` project).

---

## 📄 License

Academic/institutional project — IFRJ Eng. Paulo de Frontin Campus. Define the desired license here (e.g., MIT, GPL-3.0).
