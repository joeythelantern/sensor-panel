using System.Net.Http.Json;
using Newtonsoft.Json;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace SystemDashboard;

class Program
{
    public class StatData {
        public long Timestamp { get; set; }
        public CpuStat Cpu { get; set; } = new();
        public MemoryStat Memory { get; set; } = new();
        public GpuStat Gpu { get; set; } = new();
        public DiskStat Disk { get; set; } = new();
        public NetworkStat Network { get; set; } = new();
    }

    public class CpuStat { public double Usage { get; set; } }
    public class MemoryStat { public double Load { get; set; } }
    public class GpuStat { public int SocketPower { get; set; } public int CorePower { get; set; } }
    public class DiskStat { public long Read_bytes_per_sec { get; set; } public long Write_bytes_per_sec { get; set; } }
    public class NetworkStat { public long Bytes_sent_per_sec { get; set; } public long Bytes_received_per_sec { get; set; } }

    static async Task Main(string[] args)
    {
        using var client = new HttpClient();
        string apiUrl = "http://localhost:1337/stats";
        var currentStats = new StatData();

        await AnsiConsole.Live(CreateDashboard(currentStats))
            .StartAsync(async ctx =>
            {
                while (true)
                {
                    try 
                    {
                        var response = await client.GetAsync(apiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            // If your API returns an array, take the last one. 
                            // If it returns a single object, remove the List<> part.
                            var dataList = JsonConvert.DeserializeObject<List<StatData>>(json);
                            if (dataList != null && dataList.Any())
                            {
                                currentStats = dataList.Last();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Server might be down, we just wait for next cycle
                    }

                    ctx.UpdateTarget(CreateDashboard(currentStats));
                    await Task.Delay(5000);
                }
            });
    }

    private static Table CreateDashboard(StatData data)
    {
        // Define exactly ONE column for the main container table
        var table = new Table()
            .Centered()
            .Border(TableBorder.Rounded)
            .Width(60)
            .AddColumn(new TableColumn("[bold blue]SYSTEM MONITOR[/]").Centered());

        // --- ROW 1: CPU & MEMORY (Nested Table) ---
        var topRowContainer = new Table().NoBorder().HideHeaders().Expand()
            .AddColumn("Left")
            .AddColumn("Right");

        var cpuChart = new BarChart().Width(25).AddItem("CPU %", Math.Round(data.Cpu.Usage, 1), Color.Green);
        var memChart = new BarChart().Width(25).AddItem("MEM %", Math.Round(data.Memory.Load, 1), Color.Cyan1);
        
        topRowContainer.AddRow(cpuChart, memChart);
        
        // Add the nested table as a single row entry
        table.AddRow(topRowContainer);
        table.AddRow(new Rule().RuleStyle("grey"));

        // --- ROW 2: DISK IO ---
        double diskMb = (data.Disk.Read_bytes_per_sec + data.Disk.Write_bytes_per_sec) / 1024.0 / 1024.0;
        var diskChart = new BreakdownChart().Width(55)
            .AddItem($"{diskMb:F2} MB/s", Math.Max(diskMb, 0.001), Color.Yellow);
        table.AddRow(new Panel(diskChart).Header("Disk IO").Expand());

        // --- ROW 3: NETWORK ---
        double netKb = (data.Network.Bytes_sent_per_sec + data.Network.Bytes_received_per_sec) / 1024.0;
        var netChart = new BreakdownChart().Width(55)
            .AddItem($"{netKb:F2} KB/s", Math.Max(netKb, 0.001), Color.Purple);
        table.AddRow(new Panel(netChart).Header("Network").Expand());

        // --- ROW 4: GPU POWER ---
        int gpuW = data.Gpu.CorePower + data.Gpu.SocketPower;
        var gpuChart = new BreakdownChart().Width(55)
            .AddItem($"{gpuW} Watts", Math.Max(gpuW, 0.001), Color.Red);
        table.AddRow(new Panel(gpuChart).Header("GPU Power").Expand());

        return table;
    }
}