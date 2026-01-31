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
                new Layout("CPU").Ratio(1)
            );
    }

    static void UpdateLayout(Layout layout)
    {
        layout["CPU"].Update(BuildCpuPanel());
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
}
