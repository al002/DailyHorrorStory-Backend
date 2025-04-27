using DailyStory.Api.Data;
using DailyStory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DailyStory.Api.Services;

public class StoryService : IStoryService
{
    private readonly AppDbContext _dbContext;
    private readonly IAiGenerationService _aiService;
    private readonly ILogger<StoryService> _logger;
    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(10);

    public StoryService(AppDbContext dbContext, IAiGenerationService aiService, ILogger<StoryService> logger)
    {
        _dbContext = dbContext;
        _aiService = aiService;
        _logger = logger;
    }

    private async Task<(string Title, string Content)> GenerateStoryWithRetryAsync(CancellationToken cancellationToken)
    {
        int retryCount = 0;
        TimeSpan delay = InitialRetryDelay;

        while (true)
        {
            try
            {
                return await _aiService.GenerateStoryAsync(cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(e, "Failed to generate story after {RetryCount} attempts", retryCount);
                    throw new ApplicationException($"Failed to generate story after {retryCount} attempts", e);
                }

                _logger.LogWarning(e, "Failed to generate story (attempt {RetryCount} of {MaxRetries}), retrying in {Delay}...",
                    retryCount, MaxRetries, delay);

                await Task.Delay(delay, cancellationToken);
                delay *= 2; // 指数退避
            }
        }
    }

    public async Task<Story> GetOrCreateTodayStoryAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        _logger.LogInformation("Try to get or create {Date} story", today);

        Story? existingStory = await _dbContext.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Date == today, cancellationToken);

        if (existingStory != null)
        {
            _logger.LogInformation("Found {Date} story, Id: {StoryId}", today, existingStory.Id);
            return existingStory;
        }

        _logger.LogInformation("Try to generate {Date} new story", today);

        Story? newStory = null;

        try
        {
            (string title, string content) = await GenerateStoryWithRetryAsync(cancellationToken);

            newStory = new Story()
            {
                Title = title,
                Content = content,
                Date = today,
                AiSource = "Gemini"
            };

            _dbContext.Stories.Add(newStory);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved {Date} new story, Id: {StoryId}", today, newStory.Id);
            return newStory;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException pgEx &&
                                        pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _dbContext.Entry(newStory).State = EntityState.Detached;

            Story? concurrentlyCreatedStory = await _dbContext.Stories
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Date == today, cancellationToken);

            if (concurrentlyCreatedStory != null)
            {
                return concurrentlyCreatedStory;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create new story");
            throw new ApplicationException("Unable to create new story", e);
        }

        throw new ApplicationException("Unable to create new story");
    }

    public async Task<Story?> GetStoryByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        Story? story = await _dbContext.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Date == date, cancellationToken);

        if (story == null)
        {
            _logger.LogInformation("Find {Date} story", date);
        } else {
            _logger.LogInformation("Find {Date} story, Id: {StoryId})", date, story.Id);
        }

        return story;
    }

    public async Task<List<Story>> GetStoriesAsync(int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        const int maxPageSize = 50;
        if (pageSize > maxPageSize) pageSize = maxPageSize;

        _logger.LogInformation("Get stories, pageNumber: {PageNumber}, pageSize: {PageSize}", pageNumber, pageSize);

        List<Story> stories = await _dbContext.Stories
            .AsNoTracking()
            .OrderByDescending(s => s.Date)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Queries {Count} stories", stories.Count);

        return stories;
    }
}
