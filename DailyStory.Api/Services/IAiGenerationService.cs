namespace DailyStory.Api.Services;

public interface IAiGenerationService
{
    Task<(string Title, string Content)> GenerateStoryAsync(
        string? theme = null,
        string? modelName = null,
        CancellationToken cancellationToken = default);
}