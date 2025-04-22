using System.ComponentModel.DataAnnotations;

namespace DailyStory.Api.Options;

public class AiServiceOptions
{
    public const string SectionName = "AiService";
    
    [Required]
    public required string OpenRouterApiKey { get; set; }
    
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    
    [Required]
    public string? Model { get; set; } = "gemini-2.5-pro-preview-03-25";
    
    public string? SiteUrl { get; set; }
    public string? AppName { get; set; }

    public int MaxTokens { get; set; } = 3000;
    public double Temperature { get; set; } = 1;
}