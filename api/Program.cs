
using System.Text.Json;
using System.Text.Json.Nodes;

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

var stats = new List<JsonNode>();

app.UseCors("AllowAll");

app.MapPost("/stats", async (HttpContext ctx) =>
{
    try
    {
        var stat = await JsonSerializer.DeserializeAsync<JsonNode>(ctx.Request.Body);
        if (stat is null)
            return Results.BadRequest(new { error = "Invalid payload" });

        stats.Add(stat);

        if (stats.Count > 100)
            stats.RemoveAt(0);

        Console.WriteLine($"Received stat at {stat["timestamp"]}. Total stored: {stats.Count}");
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