using DailyStory.Api.Models;

namespace DailyStory.Api.Services;

public interface IStoryService
{
    Task<Story> GetOrCreateTodayStoryAsync(CancellationToken cancellationToken = default);
    
    Task<Story?> GetStoryByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    
    Task<List<Story>> GetStoriesAsync(int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default);
}