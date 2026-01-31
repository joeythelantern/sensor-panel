using Spectre.Console;
using System;
using System.Threading;

class Program
{
    static void Main()
    {
        var layout = BuildLayout();

        AnsiConsole.Live(layout)
            .AutoClear(false)
            .Start(ctx =>
            {
                while (true)
                {
                    UpdateLayout(layout);
                    ctx.UpdateTarget(layout);
                    Thread.Sleep(1000);
                }
            });
    }

    static Layout BuildLayout()
    {
        return new Layout("Root")
            .SplitRows(
                new Layout("Top")
                    .Ratio(2)
                    .SplitColumns(
                        new Layout("CPU"),
                        new Layout("Memory")
                    ),
                new Layout("Network").Ratio(1),
                new Layout("Disk").Ratio(1),
                new Layout("GPU").Ratio(1)
            );
    }

    static void UpdateLayout(Layout layout)
    {
        layout["CPU"].Update(BuildCpuPanel());
        layout["Memory"].Update(BuildMemoryPanel());
        layout["Network"].Update(BuildNetworkPanel());
        layout["Disk"].Update(BuildDiskPanel());
        layout["GPU"].Update(BuildGpuPanel());
    }

    static Panel BuildCpuPanel()
    {
        int usage = Random.Shared.Next(10, 100);
        int temp = Random.Shared.Next(45, 85);

        var chart = new BarChart()
            .Label("[bold]CPU Usage %[/]")
            .CenterLabel()
            .AddItem("Load", usage, Color.Red);

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(chart);
        grid.AddRow(new Markup(
            $"[yellow]Temp:[/] [bold]{temp} °C[/]\n" +
            $"[cyan]Clock:[/] [bold]4.6 GHz[/]"
        ));

        return new Panel(grid)
        {
            Header = new PanelHeader("CPU", Justify.Center),
            Border = BoxBorder.Double,
            Padding = new Padding(1, 1)
        };
    }

    static Panel BuildMemoryPanel()
    {
        int usage = Random.Shared.Next(20, 90);

        var chart = new BarChart()
            .Label("[bold]Memory Usage %[/]")
            .CenterLabel()
            .AddItem("RAM", usage, Color.Blue);

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(chart);
        grid.AddRow(new Markup(
            $"[blue]Used:[/] [bold]10.4 GB[/]\n" +
            $"[grey]Free:[/] [bold]21.6 GB[/]"
        ));

        return new Panel(grid)
        {
            Header = new PanelHeader("Memory", Justify.Center),
            Border = BoxBorder.Double,
            Padding = new Padding(1, 1)
        };
    }

    static Panel BuildNetworkPanel()
    {
        int down = Random.Shared.Next(10, 900);
        int up = Random.Shared.Next(5, 200);

        var chart = new BarChart()
            .Label("[bold]Network Mbps[/]")
            .CenterLabel()
            .AddItem("Download", down, Color.Green)
            .AddItem("Upload", up, Color.Yellow);

        return new Panel(chart)
        {
            Header = new PanelHeader("Network", Justify.Center),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 1)
        };
    }

    static Panel BuildDiskPanel()
    {
        int read = Random.Shared.Next(50, 700);
        int write = Random.Shared.Next(30, 400);

        var chart = new BarChart()
            .Label("[bold]Disk I/O MB/s[/]")
            .CenterLabel()
            .AddItem("Read", read, Color.Green)
            .AddItem("Write", write, Color.Orange1);

        return new Panel(chart)
        {
            Header = new PanelHeader("Disk I/O", Justify.Center),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 1)
        };
    }

    static Panel BuildGpuPanel()
    {
        int power = Random.Shared.Next(60, 260);

        var chart = new BarChart()
            .Label("[bold]GPU Power (W)[/]")
            .CenterLabel()
            .AddItem("Power", power, Color.Red);

        return new Panel(chart)
        {
            Header = new PanelHeader("GPU Power", Justify.Center),
            Border = BoxBorder.Double,
            Padding = new Padding(1, 1)
        };
    }
}
