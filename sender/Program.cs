using System.Text;
using Modules;
using Newtonsoft.Json;
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

                var payload = new StatData
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Cpu = GetCpuStats(computer),
                    Memory = GetMemoryStats(computer),
                    Gpu = GetGpuStats(computer),
                    Disk = GetDiskStats(),
                    Network = GetNetworkStats()
                };

                string json = JsonConvert.SerializeObject(payload);
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

    static CpuStat GetCpuStats(Computer computer)
    {
        var cpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == null) return new CpuStat();

        var usageSensor = cpu.Sensors.FirstOrDefault(s => s.Name == CPU_LOAD);

        return new CpuStat
        {
            Usage = Math.Round(usageSensor?.Value ?? 0, 2)
        };
    }

    static MemoryStat GetMemoryStats(Computer computer)
    {
        var ram = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        if (ram == null) return new MemoryStat();

        ram.Update();

        var load = ram.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == MEMORY_LOAD)?.Value ?? 0; ;

        return new MemoryStat
        {
            Load = load
        };
    }

    static GpuStat GetGpuStats(Computer computer)
    {
        var gpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd);
        if (gpu == null) return new GpuStat();

        gpu.Update();

        var socketPower = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == GPU_SOCKET_POWER);
        var corePower = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == GPU_CORE_POWER);

        return new GpuStat
        {
            SocketPower = (int)Math.Round(socketPower?.Value ?? 0),
            CorePower = (int)Math.Round(corePower?.Value ?? 0)
        };
    }

    [SupportedOSPlatform("windows")]
    static DiskStat GetDiskStats()
    {
        var diskRead = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        var diskWrite = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

        diskRead.NextValue();
        diskWrite.NextValue();
        Thread.Sleep(1000);

        return new DiskStat
        {
            Read_bytes_per_sec = (long)diskRead.NextValue(),
            Write_bytes_per_sec = (long)diskWrite.NextValue()
        };
    }

    static long prevSent = 0;
    static long prevReceived = 0;

    static NetworkStat GetNetworkStats()
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

        long deltaSent = sent - prevSent;
        long deltaReceived = received - prevReceived;

        prevSent = sent;
        prevReceived = received;

        return new NetworkStat
        {
            Bytes_sent_per_sec = deltaSent,
            Bytes_received_per_sec = deltaReceived
        };
    }

    static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}