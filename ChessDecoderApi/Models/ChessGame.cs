using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Cloud.Firestore;

namespace ChessDecoderApi.Models;

[FirestoreData]
public class ChessGame
{
    [Key]
    // NOTE: Guid Id is NOT serialized to Firestore - it's used as the document ID (converted to string)
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [FirestoreProperty]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [FirestoreProperty]
    public string Title { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public string? Description { get; set; }
    
    [Required]
    [FirestoreProperty]
    public string PgnContent { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public string? PgnOutputPath { get; set; }
    
    [FirestoreProperty]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    
    [FirestoreProperty]
    public int ProcessingTimeMs { get; set; }
    
    [FirestoreProperty]
    public bool IsValid { get; set; }
    
    [FirestoreProperty]
    public string? ValidationMessage { get; set; }
    
    // Player metadata fields for PGN format
    [FirestoreProperty]
    public string? WhitePlayer { get; set; }
    
    [FirestoreProperty]
    public string? BlackPlayer { get; set; }
    
    [FirestoreProperty]
    public DateTime? GameDate { get; set; }
    
    [FirestoreProperty]
    public string? Round { get; set; }
    
    [FirestoreProperty]
    public string? Result { get; set; }
    
    // Processing completion tracking
    [FirestoreProperty]
    public bool ProcessingCompleted { get; set; } = false;
    
    [FirestoreProperty]
    public DateTime? LastEditedAt { get; set; }
    
    [FirestoreProperty]
    public int EditCount { get; set; } = 0;
    
    // Navigation properties - NOT stored in Firestore
    [ForeignKey("UserId")]
    [NotMapped]
    public virtual User User { get; set; } = null!;
    
    [NotMapped]
    public virtual ICollection<GameImage> InputImages { get; set; } = new List<GameImage>();
    
    [NotMapped]
    public virtual GameStatistics Statistics { get; set; } = null!;
}

[FirestoreData]
public class GameImage
{
    [Key]
    // NOTE: Guid Id is NOT serialized to Firestore - it's used as the document ID (converted to string)
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    // NOTE: Guid ChessGameId is NOT serialized to Firestore - handled manually in FirestoreService
    public Guid ChessGameId { get; set; }
    
    [Required]
    [FirestoreProperty]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [FirestoreProperty]
    public string FilePath { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public string? FileType { get; set; }
    
    [FirestoreProperty]
    public long FileSizeBytes { get; set; }
    
    [FirestoreProperty]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // Cloud Storage properties
    [FirestoreProperty]
    public string? CloudStorageUrl { get; set; }
    
    [FirestoreProperty]
    public string? CloudStorageObjectName { get; set; }
    
    [FirestoreProperty]
    public bool IsStoredInCloud { get; set; } = false;

    [FirestoreProperty]
    public string Variant { get; set; } = "original"; // "original" or "processed"
    
    // Navigation property - NOT stored in Firestore
    [ForeignKey("ChessGameId")]
    [NotMapped]
    public virtual ChessGame ChessGame { get; set; } = null!;
}

[FirestoreData]
public class GameStatistics
{
    [Key]
    // NOTE: Guid Id is NOT serialized to Firestore - it's used as the document ID (converted to string)
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    // NOTE: Guid ChessGameId is NOT serialized to Firestore - handled manually in FirestoreService
    public Guid ChessGameId { get; set; }
    
    [FirestoreProperty]
    public int TotalMoves { get; set; }
    
    [FirestoreProperty]
    public int ValidMoves { get; set; }
    
    [FirestoreProperty]
    public int InvalidMoves { get; set; }
    
    [FirestoreProperty]
    public string? Opening { get; set; }
    
    [FirestoreProperty]
    public string? Result { get; set; }
    
    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property - NOT stored in Firestore
    [ForeignKey("ChessGameId")]
    [NotMapped]
    public virtual ChessGame ChessGame { get; set; } = null!;
}
