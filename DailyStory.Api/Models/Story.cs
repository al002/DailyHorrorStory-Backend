using System.ComponentModel.DataAnnotations;

namespace DailyStory.Api.Models;

public class Story
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public required string Title { get; set; }
    
    [Required]
    public string Content { get; set; }
    
    public DateOnly Date { get; set; }
    
    public string? AiSource { get; set; }
    
    public DateTime CreatedAt { get; set; }
}