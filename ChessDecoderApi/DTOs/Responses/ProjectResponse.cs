namespace ChessDecoderApi.DTOs.Responses;

/// <summary>
/// Response model for project information
/// </summary>
public class ProjectInfoResponse
{
    public Guid ProjectId { get; set; }
    public Guid GameId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public InitialUploadDto? InitialUpload { get; set; }
    public ProcessingDto? Processing { get; set; }
    public int VersionCount { get; set; }
}

/// <summary>
/// Response model for full project history
/// </summary>
public class ProjectHistoryResponse
{
    public Guid ProjectId { get; set; }
    public Guid GameId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public InitialUploadDto? InitialUpload { get; set; }
    public ProcessingDto? Processing { get; set; }
    public List<HistoryEntryDto> Versions { get; set; } = new();
}

/// <summary>
/// Initial upload information DTO
/// </summary>
public class InitialUploadDto
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string StorageLocation { get; set; } = "local";
    public string? StorageUrl { get; set; }
}

/// <summary>
/// Processing result information DTO
/// </summary>
public class ProcessingDto
{
    public DateTime ProcessedAt { get; set; }
    public string PgnContent { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = "unknown";
    public int ProcessingTimeMs { get; set; }
}

/// <summary>
/// History entry DTO
/// </summary>
public class HistoryEntryDto
{
    public int Version { get; set; }
    public DateTime Timestamp { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object>? Changes { get; set; }
}

/// <summary>
/// Response model for list of user projects
/// </summary>
public class UserProjectsResponse
{
    public List<ProjectInfoResponse> Projects { get; set; } = new();
    public int TotalCount { get; set; }
}
