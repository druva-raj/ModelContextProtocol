using McpServer.EntraID.Bearer.Models;
using McpServer.EntraID.Bearer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace McpServer.EntraID.Bearer.Tools;

/// <summary>
/// MCP tools for managing asynchronous operations using the Request-Reply pattern.
/// These tools allow clients to:
/// 1. Check the status of running jobs
/// 2. Get results of completed jobs
/// 3. List all jobs
/// 4. Cancel running jobs
/// </summary>
[McpServerToolType]
public class AsyncOperationTools
{
    private readonly IJobService _jobService;

    public AsyncOperationTools(IJobService jobService)
    {
        _jobService = jobService;
    }

    [McpServerTool(Name = "get_job_status"), Description(
        "Check the status of an asynchronous job. " +
        "Use this tool after starting a long-running operation to check if it has completed. " +
        "Returns the current status, progress percentage, and elapsed time.")]
    public string GetJobStatus(
        [Description("The job ID returned when the operation was started.")] string jobId)
    {
        var job = _jobService.GetJob(jobId);
        
        if (job == null)
        {
            return $"""
                ## Job Not Found
                
                No job found with ID: `{jobId}`
                
                The job may have expired or the ID may be incorrect.
                Use `list_jobs` to see all available jobs.
                """;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Job Status: {job.Status}");
        sb.AppendLine();
        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"|----------|-------|");
        sb.AppendLine($"| Job ID | `{job.JobId}` |");
        sb.AppendLine($"| Operation | {job.OperationType} |");
        sb.AppendLine($"| Status | **{job.Status}** |");
        sb.AppendLine($"| Progress | {job.Progress}% |");
        sb.AppendLine($"| Message | {job.StatusMessage} |");
        sb.AppendLine($"| Created | {job.CreatedAt:u} |");
        
        if (job.StartedAt.HasValue)
        {
            sb.AppendLine($"| Started | {job.StartedAt:u} |");
        }
        
        if (job.CompletedAt.HasValue)
        {
            sb.AppendLine($"| Completed | {job.CompletedAt:u} |");
        }
        
        sb.AppendLine($"| Elapsed | {job.ElapsedTime:g} |");
        sb.AppendLine();

        // Provide guidance on next steps
        switch (job.Status)
        {
            case JobStatus.Pending:
            case JobStatus.Running:
                sb.AppendLine("### Next Steps");
                sb.AppendLine("The job is still processing. Check again in a few seconds using:");
                sb.AppendLine($"```");
                sb.AppendLine($"get_job_status(jobId: \"{jobId}\")");
                sb.AppendLine($"```");
                break;
                
            case JobStatus.Completed:
                sb.AppendLine("### Next Steps");
                sb.AppendLine("The job has completed! Get the result using:");
                sb.AppendLine($"```");
                sb.AppendLine($"get_job_result(jobId: \"{jobId}\")");
                sb.AppendLine($"```");
                break;
                
            case JobStatus.Failed:
                sb.AppendLine("### Error Details");
                sb.AppendLine($"The job failed with error: {job.Error}");
                break;
                
            case JobStatus.Cancelled:
                sb.AppendLine("### Cancelled");
                sb.AppendLine("This job was cancelled before completion.");
                break;
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "get_job_result"), Description(
        "Get the result of a completed asynchronous job. " +
        "Only works for jobs that have status 'Completed'. " +
        "For jobs still running, use get_job_status instead.")]
    public string GetJobResult(
        [Description("The job ID to get the result for.")] string jobId)
    {
        var job = _jobService.GetJob(jobId);
        
        if (job == null)
        {
            return $"""
                ## Job Not Found
                
                No job found with ID: `{jobId}`
                """;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Job Result");
        sb.AppendLine();
        sb.AppendLine($"**Job ID:** `{job.JobId}`  ");
        sb.AppendLine($"**Operation:** {job.OperationType}  ");
        sb.AppendLine($"**Status:** {job.Status}  ");
        sb.AppendLine($"**Total Time:** {job.ElapsedTime:g}");
        sb.AppendLine();

        switch (job.Status)
        {
            case JobStatus.Completed:
                sb.AppendLine("### Result");
                if (job.Result != null)
                {
                    if (job.Result is string strResult)
                    {
                        sb.AppendLine(strResult);
                    }
                    else
                    {
                        sb.AppendLine("```json");
                        sb.AppendLine(JsonSerializer.Serialize(job.Result, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        }));
                        sb.AppendLine("```");
                    }
                }
                else
                {
                    sb.AppendLine("_No result data available._");
                }
                break;
                
            case JobStatus.Failed:
                sb.AppendLine("### Error");
                sb.AppendLine($"The job failed: {job.Error}");
                break;
                
            case JobStatus.Cancelled:
                sb.AppendLine("### Cancelled");
                sb.AppendLine("This job was cancelled and has no result.");
                break;
                
            default:
                sb.AppendLine("### Still Processing");
                sb.AppendLine($"This job is still {job.Status.ToString().ToLower()}. Progress: {job.Progress}%");
                sb.AppendLine();
                sb.AppendLine("Use `get_job_status` to check progress, or wait and try again.");
                break;
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "list_jobs"), Description(
        "List all asynchronous jobs. " +
        "Optionally filter by status (Pending, Running, Completed, Failed, Cancelled). " +
        "Returns the most recent jobs first.")]
    public string ListJobs(
        [Description("Optional status filter: Pending, Running, Completed, Failed, or Cancelled")] 
        string? status = null,
        [Description("Maximum number of jobs to return (default: 10)")] 
        int limit = 10)
    {
        JobStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<JobStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var jobs = _jobService.GetJobs(statusFilter, limit).ToList();

        if (jobs.Count == 0)
        {
            return statusFilter.HasValue 
                ? $"No jobs found with status '{statusFilter}'."
                : "No jobs found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Jobs List");
        sb.AppendLine();
        sb.AppendLine("| Job ID | Operation | Status | Progress | Created |");
        sb.AppendLine("|--------|-----------|--------|----------|---------|");
        
        foreach (var job in jobs)
        {
            var shortId = job.JobId.Length > 8 ? job.JobId[..8] + "..." : job.JobId;
            sb.AppendLine($"| `{shortId}` | {job.OperationType} | {job.Status} | {job.Progress}% | {job.CreatedAt:HH:mm:ss} |");
        }
        
        sb.AppendLine();
        sb.AppendLine("### Usage");
        sb.AppendLine("- Use `get_job_status(jobId)` to check a specific job's status");
        sb.AppendLine("- Use `get_job_result(jobId)` to get a completed job's result");
        sb.AppendLine("- Use `cancel_job(jobId)` to cancel a running job");

        return sb.ToString();
    }

    [McpServerTool(Name = "cancel_job"), Description(
        "Cancel a running or pending asynchronous job. " +
        "Only works for jobs that are not yet completed, failed, or already cancelled.")]
    public string CancelJob(
        [Description("The job ID to cancel.")] string jobId)
    {
        var job = _jobService.GetJob(jobId);
        
        if (job == null)
        {
            return $"""
                ## Job Not Found
                
                No job found with ID: `{jobId}`
                """;
        }

        if (job.IsTerminal)
        {
            return $"""
                ## Cannot Cancel
                
                Job `{jobId}` is already in a terminal state: **{job.Status}**
                
                Only pending or running jobs can be cancelled.
                """;
        }

        var cancelled = _jobService.CancelJob(jobId);
        
        if (cancelled)
        {
            return $"""
                ## Job Cancelled
                
                Successfully cancelled job `{jobId}`.
                
                | Property | Value |
                |----------|-------|
                | Operation | {job.OperationType} |
                | Was at | {job.Progress}% progress |
                | Ran for | {job.ElapsedTime:g} |
                """;
        }
        
        return $"Failed to cancel job `{jobId}`. It may have completed before cancellation.";
    }
}
