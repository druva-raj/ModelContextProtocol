using System.Text.Json.Serialization;

namespace McpServer.EntraID.Bearer.Models;

/// <summary>
/// Represents the status of an asynchronous job.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobStatus
{
    /// <summary>Job is waiting to be processed.</summary>
    Pending,
    
    /// <summary>Job is currently being processed.</summary>
    Running,
    
    /// <summary>Job completed successfully.</summary>
    Completed,
    
    /// <summary>Job failed with an error.</summary>
    Failed,
    
    /// <summary>Job was cancelled by the user.</summary>
    Cancelled
}

/// <summary>
/// Represents an asynchronous job in the request-reply pattern.
/// </summary>
public class AsyncJob
{
    /// <summary>
    /// Unique identifier for the job.
    /// </summary>
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    
    /// <summary>
    /// Type of operation being performed.
    /// </summary>
    public string OperationType { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of the job.
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Pending;
    
    /// <summary>
    /// Progress percentage (0-100) for operations that support progress tracking.
    /// </summary>
    public int Progress { get; set; } = 0;
    
    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string StatusMessage { get; set; } = "Job created";
    
    /// <summary>
    /// When the job was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// When the job started processing.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }
    
    /// <summary>
    /// When the job completed (success, failure, or cancellation).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
    
    /// <summary>
    /// The result of the job (available when Status is Completed).
    /// </summary>
    public object? Result { get; set; }
    
    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Input parameters that were provided to start the job.
    /// </summary>
    public Dictionary<string, object?> InputParameters { get; set; } = new();
    
    /// <summary>
    /// Optional user identifier who initiated the job.
    /// </summary>
    public string? InitiatedBy { get; set; }
    
    /// <summary>
    /// Cancellation token source for cancelling the job.
    /// </summary>
    [JsonIgnore]
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    
    /// <summary>
    /// Gets whether the job is in a terminal state.
    /// </summary>
    [JsonIgnore]
    public bool IsTerminal => Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled;
    
    /// <summary>
    /// Gets the elapsed time since the job was created.
    /// </summary>
    public TimeSpan? ElapsedTime => CompletedAt.HasValue 
        ? CompletedAt.Value - CreatedAt 
        : DateTimeOffset.UtcNow - CreatedAt;
}

/// <summary>
/// Result returned when a job is started.
/// </summary>
public record JobStartedResult(
    string JobId,
    string Message,
    string StatusCheckInstruction
);

/// <summary>
/// Result returned when checking job status.
/// </summary>
public record JobStatusResult(
    string JobId,
    JobStatus Status,
    int Progress,
    string StatusMessage,
    TimeSpan? ElapsedTime,
    bool IsComplete,
    string? NextStep
);

/// <summary>
/// Result returned when retrieving job result.
/// </summary>
public record JobResultResponse(
    string JobId,
    JobStatus Status,
    object? Result,
    string? Error,
    TimeSpan? TotalTime
);
