namespace DailyStory.Api.Services;

public class DailyStoryGeneratorService : IHostedService
{
    private readonly ILogger<DailyStoryGeneratorService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public DailyStoryGeneratorService(ILogger<DailyStoryGeneratorService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting daily story generator");

        var initialDelay = CalculateInitialDelay();
        _timer = new Timer(DoWork, null, initialDelay, _checkInterval);
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping daily story generator");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _logger.LogDebug("Disposing daily story generator");
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private TimeSpan CalculateInitialDelay()
    {
        var nowUtc = DateTime.UtcNow;
        var nextMidnightUtc = nowUtc.Date.AddDays(1);
        var delay = nextMidnightUtc - nowUtc;

        if (delay <= TimeSpan.Zero)
        {
            delay = TimeSpan.FromSeconds(1);
        }

        return delay;
    }

    private void DoWork(object? state)
    {
        var executionTime = DateTime.UtcNow;
        _logger.LogInformation("Daily story generator started, UTC {ExecutionTime}", executionTime);

        using (var scope = _scopeFactory.CreateScope())
        {
            var storyService = scope.ServiceProvider.GetRequiredService<IStoryService>();
            try
            {
                var story = storyService.GetOrCreateTodayStoryAsync().GetAwaiter().GetResult();
                _logger.LogInformation("Daily story generated, ID: {StoryId}，Date: {StoryDate}", story.Id, story.Date);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Daily story generation failed");
            }
        }
        
        _logger.LogInformation("Daily story generator finished");
    }
}