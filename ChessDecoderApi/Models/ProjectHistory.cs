using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace ChessDecoderApi.Models;

/// <summary>
/// Project history tracking for a chess game
/// </summary>
[FirestoreData]
public class ProjectHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [FirestoreProperty]
    public Guid GameId { get; set; }
    
    [Required]
    [FirestoreProperty]
    public string UserId { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [FirestoreProperty]
    public InitialUploadData? InitialUpload { get; set; }
    
    [FirestoreProperty]
    public ProcessingData? Processing { get; set; }
    
    [FirestoreProperty]
    public List<HistoryEntry> Versions { get; set; } = new();
}

/// <summary>
/// Initial upload metadata
/// </summary>
[FirestoreData]
public class InitialUploadData
{
    [FirestoreProperty]
    public string FileName { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public long FileSize { get; set; }
    
    [FirestoreProperty]
    public string FileType { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public DateTime UploadedAt { get; set; }
    
    [FirestoreProperty]
    public string StorageLocation { get; set; } = "local"; // "local" or "cloud"
    
    [FirestoreProperty]
    public string? StorageUrl { get; set; }
}

/// <summary>
/// Processing result metadata
/// </summary>
[FirestoreData]
public class ProcessingData
{
    [FirestoreProperty]
    public DateTime ProcessedAt { get; set; }
    
    [FirestoreProperty]
    public string PgnContent { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public string ValidationStatus { get; set; } = "unknown"; // "valid", "invalid", "unknown"
    
    [FirestoreProperty]
    public int ProcessingTimeMs { get; set; }
}

/// <summary>
/// Individual history entry for version tracking
/// </summary>
[FirestoreData]
public class HistoryEntry
{
    [FirestoreProperty]
    public int Version { get; set; }
    
    [FirestoreProperty]
    public DateTime Timestamp { get; set; }
    
    [FirestoreProperty]
    public string ChangeType { get; set; } = "initial_upload"; // "initial_upload", "modification", "correction", "update", "export"
    
    [FirestoreProperty]
    public string Description { get; set; } = string.Empty;
    
    [FirestoreProperty]
    public Dictionary<string, object>? Changes { get; set; }
}
