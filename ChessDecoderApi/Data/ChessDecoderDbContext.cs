using ChessDecoderApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessDecoderApi.Data;

public class ChessDecoderDbContext : DbContext
{
    public ChessDecoderDbContext(DbContextOptions<ChessDecoderDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<ChessGame> ChessGames { get; set; }
    public DbSet<GameImage> GameImages { get; set; }
    public DbSet<GameStatistics> GameStatistics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450); // For Google Auth IDs
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Picture).HasMaxLength(500);
            entity.Property(e => e.Provider).HasMaxLength(50);
            
            // Ensure email is unique
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure ChessGame entity
        modelBuilder.Entity<ChessGame>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.PgnContent).HasColumnType("text");
            entity.Property(e => e.PgnOutputPath).HasMaxLength(500);
            entity.Property(e => e.ValidationMessage).HasMaxLength(1000);
            
            // Relationship with User
            entity.HasOne(e => e.User)
                  .WithMany(u => u.ProcessedGames)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure GameImage entity
        modelBuilder.Entity<GameImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.FileType).HasMaxLength(50);
            entity.Property(e => e.Variant).HasMaxLength(20).HasDefaultValue("original");
            
            // Relationship with ChessGame
            entity.HasOne(e => e.ChessGame)
                  .WithMany(g => g.InputImages)
                  .HasForeignKey(e => e.ChessGameId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure GameStatistics entity
        modelBuilder.Entity<GameStatistics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Opening).HasMaxLength(100);
            entity.Property(e => e.Result).HasMaxLength(20);
            
            // One-to-one relationship with ChessGame
            entity.HasOne(e => e.ChessGame)
                  .WithOne(g => g.Statistics)
                  .HasForeignKey<GameStatistics>(e => e.ChessGameId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // You can add seed data here if needed
        // For example, create a default admin user
    }
}
