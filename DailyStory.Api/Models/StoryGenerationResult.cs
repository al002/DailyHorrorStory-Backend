using System.Text.Json.Serialization;

namespace DailyStory.Api.Models;

public record StoryGenerationResult()
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}