using DailyStory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyStory.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<Story> Stories { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasIndex(s => s.Date).IsUnique();

            entity.Property(s => s.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())");
        });
    }
}