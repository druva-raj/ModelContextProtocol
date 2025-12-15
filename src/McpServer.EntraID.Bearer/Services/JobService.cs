using System.Collections.Concurrent;
using McpServer.EntraID.Bearer.Models;

namespace McpServer.EntraID.Bearer.Services;

/// <summary>
/// Interface for managing asynchronous jobs in the request-reply pattern.
/// </summary>
public interface IJobService
{
    /// <summary>
    /// Creates a new job and returns it.
    /// </summary>
    AsyncJob CreateJob(string operationType, Dictionary<string, object?>? inputParameters = null, string? initiatedBy = null);
    
    /// <summary>
    /// Gets a job by its ID.
    /// </summary>
    AsyncJob? GetJob(string jobId);
    
    /// <summary>
    /// Gets all jobs, optionally filtered by status.
    /// </summary>
    IEnumerable<AsyncJob> GetJobs(JobStatus? statusFilter = null, int? limit = null);
    
    /// <summary>
    /// Updates the status of a job.
    /// </summary>
    void UpdateJobStatus(string jobId, JobStatus status, string? message = null, int? progress = null);
    
    /// <summary>
    /// Marks a job as started.
    /// </summary>
    void StartJob(string jobId);
    
    /// <summary>
    /// Completes a job with a result.
    /// </summary>
    void CompleteJob(string jobId, object? result);
    
    /// <summary>
    /// Fails a job with an error.
    /// </summary>
    void FailJob(string jobId, string error);
    
    /// <summary>
    /// Cancels a job.
    /// </summary>
    bool CancelJob(string jobId);
    
    /// <summary>
    /// Removes completed/failed jobs older than the specified age.
    /// </summary>
    int CleanupOldJobs(TimeSpan maxAge);
    
    /// <summary>
    /// Starts a long-running operation as a background task.
    /// </summary>
    Task<AsyncJob> StartOperationAsync(
        string operationType,
        Func<AsyncJob, CancellationToken, Task<object?>> operation,
        Dictionary<string, object?>? inputParameters = null,
        string? initiatedBy = null);
}

/// <summary>
/// In-memory implementation of the job service.
/// For production, consider using a distributed cache or database.
/// </summary>
public class InMemoryJobService : IJobService, IDisposable
{
    private readonly ConcurrentDictionary<string, AsyncJob> _jobs = new();
    private readonly ILogger<InMemoryJobService> _logger;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _jobRetentionPeriod = TimeSpan.FromHours(1);

    public InMemoryJobService(ILogger<InMemoryJobService> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupCallback, null, _cleanupInterval, _cleanupInterval);
    }

    public AsyncJob CreateJob(string operationType, Dictionary<string, object?>? inputParameters = null, string? initiatedBy = null)
    {
        var job = new AsyncJob
        {
            OperationType = operationType,
            InputParameters = inputParameters ?? new Dictionary<string, object?>(),
            InitiatedBy = initiatedBy,
            Status = JobStatus.Pending,
            StatusMessage = "Job created, waiting to start"
        };

        if (!_jobs.TryAdd(job.JobId, job))
        {
            throw new InvalidOperationException($"Failed to create job with ID {job.JobId}");
        }

        _logger.LogInformation("Created job {JobId} of type {OperationType}", job.JobId, operationType);
        return job;
    }

    public AsyncJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public IEnumerable<AsyncJob> GetJobs(JobStatus? statusFilter = null, int? limit = null)
    {
        var query = _jobs.Values.AsEnumerable();
        
        if (statusFilter.HasValue)
        {
            query = query.Where(j => j.Status == statusFilter.Value);
        }
        
        query = query.OrderByDescending(j => j.CreatedAt);
        
        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }
        
        return query.ToList();
    }

    public void UpdateJobStatus(string jobId, JobStatus status, string? message = null, int? progress = null)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            _logger.LogWarning("Attempted to update non-existent job {JobId}", jobId);
            return;
        }

        job.Status = status;
        
        if (message != null)
        {
            job.StatusMessage = message;
        }
        
        if (progress.HasValue)
        {
            job.Progress = Math.Clamp(progress.Value, 0, 100);
        }

        _logger.LogDebug("Updated job {JobId}: Status={Status}, Progress={Progress}%", 
            jobId, status, job.Progress);
    }

    public void StartJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return;
        }

        job.Status = JobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;
        job.StatusMessage = "Job is running";
        
        _logger.LogInformation("Started job {JobId}", jobId);
    }

    public void CompleteJob(string jobId, object? result)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return;
        }

        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.Result = result;
        job.Progress = 100;
        job.StatusMessage = "Job completed successfully";
        
        _logger.LogInformation("Completed job {JobId} in {ElapsedTime}", jobId, job.ElapsedTime);
    }

    public void FailJob(string jobId, string error)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return;
        }

        job.Status = JobStatus.Failed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.Error = error;
        job.StatusMessage = $"Job failed: {error}";
        
        _logger.LogError("Failed job {JobId}: {Error}", jobId, error);
    }

    public bool CancelJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return false;
        }

        if (job.IsTerminal)
        {
            _logger.LogWarning("Cannot cancel job {JobId} - already in terminal state {Status}", 
                jobId, job.Status);
            return false;
        }

        job.CancellationTokenSource.Cancel();
        job.Status = JobStatus.Cancelled;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.StatusMessage = "Job was cancelled";
        
        _logger.LogInformation("Cancelled job {JobId}", jobId);
        return true;
    }

    public int CleanupOldJobs(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var toRemove = _jobs
            .Where(kvp => kvp.Value.IsTerminal && kvp.Value.CompletedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var jobId in toRemove)
        {
            if (_jobs.TryRemove(jobId, out var job))
            {
                job.CancellationTokenSource.Dispose();
            }
        }

        if (toRemove.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old jobs", toRemove.Count);
        }

        return toRemove.Count;
    }

    public async Task<AsyncJob> StartOperationAsync(
        string operationType,
        Func<AsyncJob, CancellationToken, Task<object?>> operation,
        Dictionary<string, object?>? inputParameters = null,
        string? initiatedBy = null)
    {
        var job = CreateJob(operationType, inputParameters, initiatedBy);
        
        // Start the operation in the background
        _ = Task.Run(async () =>
        {
            try
            {
                StartJob(job.JobId);
                var result = await operation(job, job.CancellationTokenSource.Token);
                
                if (!job.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    CompleteJob(job.JobId, result);
                }
            }
            catch (OperationCanceledException)
            {
                if (job.Status != JobStatus.Cancelled)
                {
                    job.Status = JobStatus.Cancelled;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    job.StatusMessage = "Job was cancelled";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed with exception", job.JobId);
                FailJob(job.JobId, ex.Message);
            }
        });

        return job;
    }

    private void CleanupCallback(object? state)
    {
        try
        {
            CleanupOldJobs(_jobRetentionPeriod);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during job cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        foreach (var job in _jobs.Values)
        {
            job.CancellationTokenSource.Dispose();
        }
        _jobs.Clear();
    }
}
