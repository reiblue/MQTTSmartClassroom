using System;
using System.Collections.Generic;
using OpenHardwareMonitor.Hardware;
using System.Linq;
using System.Net.NetworkInformation; // Para Environment.MachineName

public class CpuTemperatureMonitor
{
    public class TemperatureReading
    {
        public string ComputerName { get; set; }
        public string SensorName { get; set; }
        public double TemperatureCelsius { get; set; }
        public string Source { get; set; }
        public DateTime Timestamp { get; set; }

        // Construtor privado para forçar o uso do método de fábrica
        private TemperatureReading()
        {
            // Opcional: Impedir criação direta sem o método Create
            // Se você quiser que o construtor padrão seja acessível, pode remover este construtor privado.
        }

        // Método de fábrica estático para criar uma nova instância de TemperatureReading
        public static TemperatureReading Create(string sensorName, double temperatureCelsius, string source)
        {
            return new TemperatureReading
            {
                ComputerName = Environment.MachineName, // Coleta o nome do computador aqui
                SensorName = sensorName,
                TemperatureCelsius = temperatureCelsius,
                Source = source,
                Timestamp = DateTime.Now // Coleta o timestamp aqui
            };
        }
    }

    public TemperatureReading GetOverallCpuTemperatureOpenHardware()
    {
        Computer computer = null;

        try
        {
            computer = new Computer() { CPUEnabled = true };
            computer.Open();

            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.CPU)
                {
                    hardware.Update();

                    ISensor cpuPackageTempSensor = hardware.Sensors.FirstOrDefault(s =>
                        s.SensorType == SensorType.Temperature &&
                        s.Name != null &&
                        (s.Name.Equals("Package", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("CPU", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                    );

                    if (cpuPackageTempSensor != null && cpuPackageTempSensor.Value.HasValue)
                    {
                        // CHAMA O MÉTODO DE FÁBRICA AQUI
                        return TemperatureReading.Create(
                            sensorName: $"{hardware.Name} {cpuPackageTempSensor.Name}",
                            temperatureCelsius: cpuPackageTempSensor.Value.Value,
                            source: "OpenHardwareMonitorLib"
                        );
                    }

                    var coreTemps = hardware.Sensors.Where(s =>
                        s.SensorType == SensorType.Temperature &&
                        s.Name != null &&
                        s.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 && s.Value.HasValue)
                        .Select(s => s.Value.Value)
                        .ToList();

                    if (coreTemps.Any())
                    {
                        // CHAMA O MÉTODO DE FÁBRICA AQUI
                        return TemperatureReading.Create(
                            sensorName: $"{hardware.Name} (Cores Average)",
                            temperatureCelsius: coreTemps.Average(),
                            source: "OpenHardwareMonitorLib"
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao coletar temperatura da CPU via OpenHardwareMonitor: {ex.Message}");
        }
        finally
        {
            if (computer != null)
            {
                computer.Close();
            }
        }
        return null;
    }

   
}