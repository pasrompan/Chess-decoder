using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessDecoderApi.Models;

public class ChessGame
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string PgnContent { get; set; } = string.Empty;
    
    public string? PgnOutputPath { get; set; }
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    
    public int ProcessingTimeMs { get; set; }
    
    public bool IsValid { get; set; }
    
    public string? ValidationMessage { get; set; }
    
    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
    
    public virtual ICollection<GameImage> InputImages { get; set; } = new List<GameImage>();
    
    public virtual GameStatistics Statistics { get; set; } = null!;
}

public class GameImage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid ChessGameId { get; set; }
    
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string FilePath { get; set; } = string.Empty;
    
    public string? FileType { get; set; }
    
    public long FileSizeBytes { get; set; }
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // Cloud Storage properties
    public string? CloudStorageUrl { get; set; }
    
    public string? CloudStorageObjectName { get; set; }
    
    public bool IsStoredInCloud { get; set; } = false;
    
    // Navigation property
    [ForeignKey("ChessGameId")]
    public virtual ChessGame ChessGame { get; set; } = null!;
}

public class GameStatistics
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid ChessGameId { get; set; }
    
    public int TotalMoves { get; set; }
    
    public int ValidMoves { get; set; }
    
    public int InvalidMoves { get; set; }
    
    public string? Opening { get; set; }
    
    public string? Result { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    [ForeignKey("ChessGameId")]
    public virtual ChessGame ChessGame { get; set; } = null!;
}
