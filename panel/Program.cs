using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Newtonsoft.Json;
using Modules;
using Spectre.Console.Rendering;

namespace SensorPanel
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static string apiUrl = "http://localhost:1337/stats";

        static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                apiUrl = args[0];
            }

            await RunSensorPanel();
        }

        static async Task RunSensorPanel()
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Clear screen once at startup
            AnsiConsole.Clear();
            AnsiConsole.Cursor.Hide();

            await AnsiConsole.Live(new Panel("Loading..."))
                .StartAsync(async ctx =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var readings = await FetchSensorData();
                            if (readings != null && readings.Count > 0)
                            {
                                var display = BuildDisplay(readings);
                                ctx.UpdateTarget(display);
                            }
                            await Task.Delay(2000, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            ctx.UpdateTarget(new Panel($"[red]Error: {ex.Message}[/]\n\nRetrying...")
                                .BorderColor(Color.Red));
                            await Task.Delay(5000, cts.Token);
                        }
                    }
                });

            AnsiConsole.Cursor.Show();
            AnsiConsole.MarkupLine("\n[yellow]Sensor panel stopped.[/]");
        }

        static async Task<List<StatData>> FetchSensorData()
        {
            var response = await httpClient.GetStringAsync(apiUrl);
            var readings = JsonConvert.DeserializeObject<List<StatData>>(response);
            return readings;
        }

        static Layout BuildDisplay(List<StatData> readings)
        {
            var latest = readings[0]; // Index 0 is newest
            var historyCount = Math.Min(50, readings.Count);
            var history = readings.Take(historyCount).Reverse().ToList();

            var grid = new Grid();
            grid.AddColumn(new GridColumn());

            var timestamp = new Markup($"[grey]{DateTimeOffset.FromUnixTimeSeconds(latest.Timestamp).LocalDateTime:yyyy-MM-dd HH:mm:ss}[/]")
                .Centered();

            // CPU Panel
            var cpuPanel = CreateCpuPanel(latest, history);

            // Memory Panel
            var memoryPanel = CreateMemoryPanel(latest, history);

            // Disk Panel
            var diskPanel = CreateDiskPanel(latest, history);

            // Network Panel
            var networkPanel = CreateNetworkPanel(latest, history);

            // GPU Panel (if applicable)
            var gpuPanel = CreateGpuPanel(latest, history);

            grid.AddRow(cpuPanel);
            grid.AddRow(memoryPanel);
            grid.AddRow(diskPanel);
            grid.AddRow(networkPanel);
            grid.AddRow(gpuPanel);

            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Header").Size(3),
                    new Layout("Body")
                );

            layout["Header"].Update(
                new Panel(
                    Align.Center(timestamp, VerticalAlignment.Middle))
                    .Expand()
                    .BorderColor(Color.Cyan1)
                    .Border(BoxBorder.Rounded));

            layout["Body"].Update(grid);

            return layout;
        }

        static Panel CreateCpuPanel(StatData latest, List<StatData> history)
        {
            var cpuUsage = latest.Cpu.Usage;
            var color = GetColorForPercentage(cpuUsage);

            var sparklineData = history.Select(r => r.Cpu.Usage).ToList();
            var sparkline = CreateSparkline(sparklineData, 80);

            var breakdown = new BreakdownChart()
                .Width(80)
                .AddItem("Used", cpuUsage, color)
                .AddItem("Free", 100 - cpuUsage, Color.Grey23);

            var content = new Rows(
                new Markup($"[bold {color}]{cpuUsage:F2}%[/]"),
                new Rule().RuleStyle($"{color} dim"),
                breakdown,
                new Text(""),
                new Markup($"[grey]Trend:[/] {sparkline}")
            );

            return new Panel(content)
                .Header("CPU Usage", Justify.Center)
                .BorderColor(color)
                .Border(BoxBorder.Rounded);
        }

        static Panel CreateMemoryPanel(StatData latest, List<StatData> history)
        {
            var memLoad = latest.Memory.Load;
            var color = GetColorForPercentage(memLoad);

            var sparklineData = history.Select(r => r.Memory.Load).ToList();
            var sparkline = CreateSparkline(sparklineData, 80);

            var breakdown = new BreakdownChart()
                .Width(80)
                .AddItem("Used", memLoad, color)
                .AddItem("Free", 100 - memLoad, Color.Grey23);

            var content = new Rows(
                new Markup($"[bold {color}]{memLoad:F2}%[/]"),
                new Rule().RuleStyle($"{color} dim"),
                breakdown,
                new Text(""),
                new Markup($"[grey]Trend:[/] {sparkline}")
            );

            return new Panel(content)
                .Header("Memory Load", Justify.Center)
                .BorderColor(color)
                .Border(BoxBorder.Rounded);
        }

        static Panel CreateDiskPanel(StatData latest, List<StatData> history)
        {
            var readMB = latest.Disk.Read_bytes_per_sec / 1024.0 / 1024.0;
            var writeMB = latest.Disk.Write_bytes_per_sec / 1024.0 / 1024.0;

            var readSparkline = CreateSparkline(
                history.Select(r => r.Disk.Read_bytes_per_sec / 1024.0 / 1024.0).ToList(), 80);
            var writeSparkline = CreateSparkline(
                history.Select(r => r.Disk.Write_bytes_per_sec / 1024.0 / 1024.0).ToList(), 80);

            var barChart = new BarChart()
                .Width(80)
                .Label("[bold]Current I/O[/]")
                .AddItem("Read", (int)Math.Min(readMB, 100), Color.Green)
                .AddItem("Write", (int)Math.Min(writeMB, 100), Color.Yellow);

            var content = new Rows(
                new Markup($"[bold green]↓ {FormatBytes(latest.Disk.Read_bytes_per_sec)}/s[/]"),
                new Markup($"[grey]Trend:[/] {readSparkline}"),
                new Text(""),
                new Markup($"[bold yellow]↑ {FormatBytes(latest.Disk.Write_bytes_per_sec)}/s[/]"),
                new Markup($"[grey]Trend:[/] {writeSparkline}")
            );

            return new Panel(content)
                .Header("Disk I/O", Justify.Center)
                .BorderColor(Color.Orange1)
                .Border(BoxBorder.Rounded);
        }

        static Panel CreateNetworkPanel(StatData latest, List<StatData> history)
        {
            var sentMB = latest.Network.Bytes_sent_per_sec / 1024.0 / 1024.0;
            var recvMB = latest.Network.Bytes_received_per_sec / 1024.0 / 1024.0;

            var sentSparkline = CreateSparkline(
                history.Select(r => r.Network.Bytes_sent_per_sec / 1024.0 / 1024.0).ToList(), 80);
            var recvSparkline = CreateSparkline(
                history.Select(r => r.Network.Bytes_received_per_sec / 1024.0 / 1024.0).ToList(), 80);

            var content = new Rows(
                new Markup($"[bold blue]↑ {FormatBytes(latest.Network.Bytes_sent_per_sec)}/s[/]"),
                new Markup($"[grey]Trend:[/] {sentSparkline}"),
                new Text(""),
                new Markup($"[bold cyan]↓ {FormatBytes(latest.Network.Bytes_received_per_sec)}/s[/]"),
                new Markup($"[grey]Trend:[/] {recvSparkline}")
            );

            return new Panel(content)
                .Header("Network I/O", Justify.Center)
                .BorderColor(Color.Blue)
                .Border(BoxBorder.Rounded);
        }

        static Panel CreateGpuPanel(StatData latest, List<StatData> history)
        {
            var socketPower = latest.Gpu.SocketPower;
            var corePower = latest.Gpu.CorePower;

            IRenderable content;

            if (socketPower == 0 && corePower == 0)
            {
                content = new Markup("[grey]No GPU data available[/]");
            }
            else
            {
                var socketSparkline = CreateSparkline(
                    history.Select(r => (double)r.Gpu.SocketPower).ToList(), 80);
                var coreSparkline = CreateSparkline(
                    history.Select(r => (double)r.Gpu.CorePower).ToList(), 80);

                content = new Rows(
                    new Markup($"[bold purple]Socket: {socketPower}W[/]"),
                    new Markup($"[grey]Trend:[/] {socketSparkline}"),
                    new Text(""),
                    new Markup($"[bold magenta]Core: {corePower}W[/]"),
                    new Markup($"[grey]Trend:[/] {coreSparkline}")
                );
            }

            return new Panel(content)
                .Header("GPU Power", Justify.Center)
                .BorderColor(Color.Purple)
                .Border(BoxBorder.Rounded);
        }

        static string CreateSparkline(List<double> data, int width)
        {
            if (data.Count == 0) return new string(' ', width);

            var chars = new[] { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█" };
            var max = data.Max();
            var min = data.Min();
            var range = max - min;

            if (range == 0) return new string('▄', Math.Min(data.Count, width));

            var result = "";
            var step = Math.Max(1, data.Count / width);

            for (int i = 0; i < data.Count && result.Length < width; i += step)
            {
                var normalized = (data[i] - min) / range;
                var index = Math.Min((int)(normalized * chars.Length), chars.Length - 1);
                result += chars[index];
            }

            return result.PadRight(width);
        }

        static Color GetColorForPercentage(double percentage)
        {
            if (percentage < 50) return Color.Green;
            if (percentage < 75) return Color.Yellow;
            if (percentage < 90) return Color.Orange1;
            return Color.Red;
        }

        static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
