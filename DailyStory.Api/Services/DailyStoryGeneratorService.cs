namespace DailyStory.Api.Services;

public class DailyStoryGeneratorService : IHostedService
{
    private readonly ILogger<DailyStoryGeneratorService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);
    private static readonly TimeOnly TargetTimeUtc = new(12, 0); // UTC 12:00 (Beijing 20:00)

    public DailyStoryGeneratorService(ILogger<DailyStoryGeneratorService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting daily story generator, will generate at {Time} UTC daily", TargetTimeUtc);

        DateTime nowUtc = DateTime.UtcNow;
        DateTime targetTimeToday = DateTime.UtcNow.Date.Add(TargetTimeUtc.ToTimeSpan());

        // 如果当前时间已经过了今天的目标时间，立即生成一次
        if (nowUtc > targetTimeToday)
        {
            _logger.LogInformation("Current time {CurrentTime} UTC is past today's target time {TargetTime} UTC, generating story immediately",
                nowUtc, targetTimeToday);
            await DoWorkAsync();
        }

        TimeSpan initialDelay = CalculateInitialDelay();
        _timer = new Timer(DoWorkCallback, null, initialDelay, _checkInterval);

        _logger.LogInformation("Next story will be generated in {Delay}", initialDelay);
        return;
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
        DateTime nowUtc = DateTime.UtcNow;
        DateTime targetTimeToday = DateTime.UtcNow.Date.Add(TargetTimeUtc.ToTimeSpan());

        TimeSpan delay = targetTimeToday - nowUtc;

        // 如果今天的目标时间已经过了，就等到明天的目标时间
        if (delay <= TimeSpan.Zero)
        {
            delay = delay.Add(_checkInterval);
        }

        return delay;
    }

    private void DoWorkCallback(object? state)
    {
        DoWorkAsync().GetAwaiter().GetResult();
    }

    private async Task DoWorkAsync()
    {
        DateTime executionTime = DateTime.UtcNow;
        _logger.LogInformation("Daily story generator started at {ExecutionTime} UTC", executionTime);

        using (IServiceScope scope = _scopeFactory.CreateScope())
        {
            IStoryService storyService = scope.ServiceProvider.GetRequiredService<IStoryService>();
            try
            {
                Models.Story story = await storyService.GetOrCreateTodayStoryAsync();
                _logger.LogInformation("Daily story generated, ID: {StoryId}, Date: {StoryDate}", story.Id, story.Date);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Daily story generation failed");
            }
        }

        _logger.LogInformation("Daily story generator finished");
    }
}
