using System;
using OpenHardwareMonitor.Hardware; // Certifique-se de que a DLL está referenciada
using System.Linq; // Para usar .FirstOrDefault()

public class CpuTemperatureMonitor
{
    // ... (Sua classe TemperatureReading e outros métodos GetTemperatures() se quiser) ...

    public static double? GetOverallCpuTemperatureOpenHardware()
    {
        using (Computer computer = new Computer()
        {
            CPUEnabled = true // Apenas habilita a CPU para focar
        })
        {
            computer.Open();
            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.CPU)
                {
                    hardware.Update(); // Atualiza os valores dos sensores

                    // Procurar por um sensor que represente a temperatura geral da CPU
                    // Nomes comuns: "Package", "CPU Package", "CPU Total", "CPU"
                    // Ou o tipo de sensor SensorType.Temperature com um nome mais genérico.
                    ISensor cpuPackageTempSensor = hardware.Sensors.FirstOrDefault(s =>
                        s.SensorType == SensorType.Temperature &&
                        (s.Name.Equals("Package", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("CPU", StringComparison.OrdinalIgnoreCase))); // A última condição pode ser muito genérica, cuidado

                    if (cpuPackageTempSensor != null && cpuPackageTempSensor.Value.HasValue)
                    {
                        return cpuPackageTempSensor.Value.Value; // Retorna a temperatura em Celsius
                    }

                    // Se não encontrar o "Package", tente a média dos cores, se disponíveis
                    // Esta é uma alternativa se não houver um sensor de pacote explícito.
                    var coreTemps = hardware.Sensors.Where(s =>
                        s.SensorType == SensorType.Temperature &&
                        s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && s.Value.HasValue)
                        .Select(s => s.Value.Value)
                        .ToList();

                    if (coreTemps.Any())
                    {
                        return coreTemps.Average();
                    }
                }
            }
        }
        return null; // Retorna null se não conseguir encontrar a temperatura
    }

    public static void Main(string[] args)
    {
        double? cpuTemp = GetOverallCpuTemperatureOpenHardware();
        if (cpuTemp.HasValue)
        {
            Console.WriteLine($"Temperatura da CPU (Geral/Pacote): {cpuTemp.Value:F2} °C");
        }
        else
        {
            Console.WriteLine("Não foi possível obter a temperatura geral da CPU via OpenHardwareMonitor.");
        }
    }
}