using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;

class Sender
{
    const string CPU_LOAD = "CPU Total";
    const string MEMORY_USED = "Memory Used";
    const string MEMORY_AVAILABLE = "Memory Available";
    const string MEMORY_LOAD = "Memory";
    const string GPU_SOCKET_POWER = "GPU SoC";
    const string GPU_CORE_POWER = "GPU Core";

    static async Task Main(string[] args)
    {
        string? apiBase = GetArg(args, "--api");
        if (string.IsNullOrEmpty(apiBase))
        {
            Console.WriteLine("Usage: SystemMonitor --api http://localhost:1337");
            return;
        }

        string endpoint = $"{apiBase.TrimEnd('/')}/stats";
        using HttpClient client = new();

        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("This sender only runs on Windows.");
            return;
        }

        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
        computer.Open();

        while (true)
        {
            try
            {
                foreach (var hardware in computer.Hardware)
                    hardware.Update();

                var payload = new
                {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    cpu = GetCpuStats(computer),
                    memory = GetMemoryStats(computer),
                    gpu = GetGpuStats(computer),
                    disk = GetDiskStats(),
                    network = GetNetworkStats()
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync(endpoint, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    static object GetCpuStats(Computer computer)
    {
        var cpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == null) return new { usage = 0.0, temperatures = Array.Empty<double>() };

        var usageSensor = cpu.Sensors.FirstOrDefault(s => s.Name == CPU_LOAD);

        return new
        {
            usage = Math.Round(usageSensor?.Value ?? 0, 2)
        };
    }

    static object GetMemoryStats(Computer computer)
    {
        var ram = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        if (ram == null) return new { percent = 0.0, total_bytes = 0L, used_bytes = 0L };

        ram.Update();

        var used = ram.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name == MEMORY_USED)?.Value ?? 0;
        var available = ram.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name == MEMORY_AVAILABLE)?.Value ?? 1;
        var load = ram.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == MEMORY_LOAD)?.Value ?? 0; ;

        return new
        {
            used,
            available,
            load
        };
    }

    static object GetGpuStats(Computer computer)
    {
        var gpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd);
        if (gpu == null) return new { temperature = (double?)null, power = 0.0, memory_total = 0L, memory_used = 0L };

        gpu.Update();

        var socketPower = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == GPU_SOCKET_POWER);
        var corePower = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == GPU_CORE_POWER);

        return new
        {
            socketPower = Math.Round(socketPower?.Value ?? 0, 2),
            corePower = Math.Round(corePower?.Value ?? 0, 2)
        };
    }

    [SupportedOSPlatform("windows")]
    static object GetDiskStats()
    {
        var diskRead = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        var diskWrite = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

        diskRead.NextValue();
        diskWrite.NextValue();
        Thread.Sleep(1000);

        return new
        {
            read_bytes_per_sec = (long)diskRead.NextValue(),
            write_bytes_per_sec = (long)diskWrite.NextValue()
        };
    }

    static long prevSent = 0;
    static long prevReceived = 0;

    static object GetNetworkStats()
    {
        long sent = 0, received = 0;

        foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            var stats = ni.GetIPv4Statistics();
            sent += stats.BytesSent;
            received += stats.BytesReceived;
        }

        // Calculate delta since last call
        long deltaSent = sent - prevSent;
        long deltaReceived = received - prevReceived;

        // Update previous values for next call
        prevSent = sent;
        prevReceived = received;

        return new
        {
            bytes_sent_per_sec = deltaSent,
            bytes_received_per_sec = deltaReceived
        };
    }

    static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}