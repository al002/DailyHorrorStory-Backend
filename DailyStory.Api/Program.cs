using DailyStory.Api.Data;
using DailyStory.Api.Options;
using DailyStory.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.Configure<AiServiceOptions>(
    builder.Configuration.GetSection(AiServiceOptions.SectionName));
builder.Services.AddScoped<IAiGenerationService, AiGenerationService>();
builder.Services.AddScoped<IStoryService, StoryService>();
builder.Services.AddHostedService<DailyStoryGeneratorService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJsDev", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // 替换为你的 Next.js 开发端口
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AllowNextJsDev");
}

app.UseHttpsRedirection();

app.MapGet("/api/ping", () => Results.Ok("Pong!"))
    .WithName("Ping")
    .WithTags("Health")
    .WithOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/test/trigger-daily-generation",
            async (IStoryService storyService, ILogger<Program> logger, CancellationToken cancellationToken) =>
            {
                logger.LogInformation("Triggering daily-generation");
                try
                {
                    var story = await storyService.GetOrCreateTodayStoryAsync(cancellationToken);
                    return Results.Ok(story);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error occurred during daily-generation");

                    return Results.Problem(
                        detail: $"Generation error: {e.Message}",
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Generation failed"
                    );
                }
            })
        .WithName("TriggerDailyStoryGenerationForTesting")
        .WithTags("Testing");
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}