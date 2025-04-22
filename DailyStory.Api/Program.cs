using DailyStory.Api.Data;
using DailyStory.Api.Models;
using DailyStory.Api.Options;
using DailyStory.Api.Services;
using Microsoft.AspNetCore.Mvc;
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
        policy.WithOrigins("http://localhost:3000")
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

app.MapGet("/api/stories", async (
        IStoryService storyService,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
    {
        try
        {
            var stories = await storyService.GetStoriesAsync(page, pageSize, cancellationToken);
            return Results.Ok(stories);
        }
        catch (Exception e)
        {
            return Results.Problem(
                detail: e.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to retrieve stories"
            );
        }
    })
    .WithName("GetStories")
    .WithTags("Stories")
    .WithOpenApi();

app.MapGet("/api/story/{date?}", async (
        IStoryService storyService,
        string? date,
        CancellationToken cancellationToken = default) =>
    {
        try
        {
            Story? story;
            
            if (string.IsNullOrEmpty(date))
            {
                var stories = await storyService.GetStoriesAsync(1, 1, cancellationToken);
                story = stories.FirstOrDefault();
            }
            else
            {
                if (!DateOnly.TryParse(date, out var parsedDate))
                {
                    return Results.BadRequest(new { error = "Invalid date format. Use YYYY-MM-DD" });
                }
                
                story = await storyService.GetStoryByDateAsync(parsedDate, cancellationToken);
            }

            if (story == null)
            {
                return Results.NotFound(new { error = "Story not found" });
            }

            return Results.Ok(story);
        }
        catch (Exception e)
        {
            return Results.Problem(
                detail: e.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to retrieve story"
            );
        }
    })
    .WithName("GetStory")
    .WithTags("Stories")
    .WithOpenApi();

app.Run();