
using Modules;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

var stats = new List<StatData>();

app.UseCors("AllowAll");

app.MapPost("/stats", async (HttpContext ctx) =>
{
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var json = await reader.ReadToEndAsync();
        var stat = JsonConvert.DeserializeObject<StatData>(json);
        if (stat is null)
            return Results.BadRequest(new { error = "Invalid payload" });

        stats.Insert(0, stat);

        if (stats.Count > 100)
            stats.RemoveAt(99);

        Console.WriteLine($"Received stat at {stat.Timestamp}. Total stored: {stats.Count}");
        return Results.Ok(new { message = "Stat saved", count = stats.Count });
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON" });
    }
});

app.MapGet("/stats", () => stats);
app.MapGet("/", () => new { count = stats.Count });
app.Run();