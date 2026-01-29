using McpServer.EntraID.Bearer.Models;
using McpServer.EntraID.Bearer.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace McpServer.EntraID.Bearer.Tools;

/// <summary>
/// Example tools that demonstrate the Async Request-Reply pattern for long-running operations.
/// These tools return immediately with a job ID, and the actual work is done in the background.
/// </summary>
[McpServerToolType]
public class LongRunningTools
{
    private readonly IJobService _jobService;
    private readonly ILogger<LongRunningTools> _logger;

    public LongRunningTools(IJobService jobService, ILogger<LongRunningTools> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    [McpServerTool(Name = "start_data_analysis"), Description(
        "Start a long-running data analysis operation. " +
        "This returns immediately with a job ID. " +
        "Use get_job_status to check progress and get_job_result when complete.")]
    public async Task<string> StartDataAnalysis(
        [Description("The dataset name to analyze")] string datasetName,
        [Description("Type of analysis: 'basic', 'detailed', or 'comprehensive'")] string analysisType = "basic",
        [Description("Number of records to process (simulated)")] int recordCount = 100)
    {
        // Validate inputs
        var validTypes = new[] { "basic", "detailed", "comprehensive" };
        if (!validTypes.Contains(analysisType.ToLower()))
        {
            return $"Invalid analysis type. Must be one of: {string.Join(", ", validTypes)}";
        }

        recordCount = Math.Clamp(recordCount, 1, 10000);

        // Start the operation asynchronously
        var job = await _jobService.StartOperationAsync(
            operationType: "DataAnalysis",
            inputParameters: new Dictionary<string, object?>
            {
                ["datasetName"] = datasetName,
                ["analysisType"] = analysisType,
                ["recordCount"] = recordCount
            },
            operation: async (job, ct) =>
            {
                _logger.LogInformation("Starting data analysis for {Dataset}", datasetName);
                
                // Simulate processing records in batches
                var batchSize = Math.Max(1, recordCount / 10);
                var processedCount = 0;
                var results = new List<string>();

                for (int batch = 0; batch < 10 && !ct.IsCancellationRequested; batch++)
                {
                    // Simulate work
                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    
                    processedCount += batchSize;
                    var progress = (int)((double)processedCount / recordCount * 100);
                    
                    _jobService.UpdateJobStatus(
                        job.JobId,
                        JobStatus.Running,
                        $"Processing batch {batch + 1}/10 - {processedCount}/{recordCount} records",
                        progress);
                    
                    // Generate some fake analysis results
                    results.Add($"Batch {batch + 1}: Found {Random.Shared.Next(1, 20)} patterns");
                }

                ct.ThrowIfCancellationRequested();

                // Generate final analysis result
                return new
                {
                    Dataset = datasetName,
                    AnalysisType = analysisType,
                    RecordsProcessed = recordCount,
                    Summary = $"Analysis complete. Found {results.Count * Random.Shared.Next(5, 15)} total patterns.",
                    TopFindings = new[]
                    {
                        $"Peak activity detected in {datasetName} sector A",
                        $"Anomaly rate: {Random.Shared.NextDouble() * 5:F2}%",
                        $"Data quality score: {Random.Shared.Next(85, 100)}%"
                    },
                    BatchResults = results
                };
            });

        return $"""
            ## Data Analysis Started
            
            Your analysis job has been queued and is starting now.
            
            | Property | Value |
            |----------|-------|
            | Job ID | `{job.JobId}` |
            | Dataset | {datasetName} |
            | Analysis Type | {analysisType} |
            | Records to Process | {recordCount:N0} |
            
            ### Next Steps
            
            1. **Check progress** with:
               ```
               get_job_status(jobId: "{job.JobId}")
               ```
            
            2. **Get results** when complete:
               ```
               get_job_result(jobId: "{job.JobId}")
               ```
            
            3. **Cancel if needed**:
               ```
               cancel_job(jobId: "{job.JobId}")
               ```
            
            _Estimated time: {recordCount / 100 * 5} seconds_
            """;
    }

    [McpServerTool(Name = "start_report_generation"), Description(
        "Start generating a comprehensive report. " +
        "Reports take time to generate and compile. " +
        "This returns immediately with a job ID for tracking.")]
    public async Task<string> StartReportGeneration(
        [Description("Title of the report")] string title,
        [Description("Report format: 'summary', 'detailed', or 'executive'")] string format = "summary",
        [Description("Include charts and visualizations")] bool includeCharts = true)
    {
        var job = await _jobService.StartOperationAsync(
            operationType: "ReportGeneration",
            inputParameters: new Dictionary<string, object?>
            {
                ["title"] = title,
                ["format"] = format,
                ["includeCharts"] = includeCharts
            },
            operation: async (job, ct) =>
            {
                var steps = new[]
                {
                    ("Gathering data sources", 15),
                    ("Analyzing metrics", 30),
                    ("Generating insights", 50),
                    ("Creating visualizations", 70),
                    ("Compiling report", 85),
                    ("Finalizing document", 95)
                };

                foreach (var (step, progress) in steps)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    _jobService.UpdateJobStatus(job.JobId, JobStatus.Running, step, progress);
                    await Task.Delay(TimeSpan.FromMilliseconds(800), ct);
                }

                // Generate the report content
                var sb = new StringBuilder();
                sb.AppendLine($"# {title}");
                sb.AppendLine();
                sb.AppendLine($"*Generated on {DateTimeOffset.UtcNow:f}*");
                sb.AppendLine();
                sb.AppendLine("## Executive Summary");
                sb.AppendLine();
                sb.AppendLine("This report provides a comprehensive analysis of the requested data.");
                sb.AppendLine($"Format: {format}");
                sb.AppendLine();
                sb.AppendLine("## Key Metrics");
                sb.AppendLine();
                sb.AppendLine("| Metric | Value | Change |");
                sb.AppendLine("|--------|-------|--------|");
                sb.AppendLine($"| Total Revenue | ${Random.Shared.Next(100000, 999999):N0} | +{Random.Shared.Next(1, 30)}% |");
                sb.AppendLine($"| Active Users | {Random.Shared.Next(1000, 50000):N0} | +{Random.Shared.Next(1, 50)}% |");
                sb.AppendLine($"| Conversion Rate | {Random.Shared.NextDouble() * 10:F2}% | +{Random.Shared.NextDouble() * 2:F2}% |");
                
                if (includeCharts)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Visualizations");
                    sb.AppendLine();
                    sb.AppendLine("📊 Revenue Trend Chart - [View]");
                    sb.AppendLine("📈 User Growth Chart - [View]");
                    sb.AppendLine("🥧 Market Share Pie Chart - [View]");
                }

                sb.AppendLine();
                sb.AppendLine("## Recommendations");
                sb.AppendLine();
                sb.AppendLine("1. Focus on high-growth segments");
                sb.AppendLine("2. Optimize conversion funnel");
                sb.AppendLine("3. Expand into emerging markets");

                return sb.ToString();
            });

        return $"""
            ## Report Generation Started
            
            Your report is being generated in the background.
            
            | Property | Value |
            |----------|-------|
            | Job ID | `{job.JobId}` |
            | Title | {title} |
            | Format | {format} |
            | Include Charts | {(includeCharts ? "Yes" : "No")} |
            
            ### Track Progress
            
            Check status: `get_job_status(jobId: "{job.JobId}")`
            
            _Estimated time: 5-10 seconds_
            """;
    }

    [McpServerTool(Name = "start_batch_processing"), Description(
        "Start a batch processing job that handles multiple items. " +
        "Useful for bulk operations that take time to complete.")]
    public async Task<string> StartBatchProcessing(
        [Description("Comma-separated list of item IDs to process")] string itemIds,
        [Description("Operation to perform: 'validate', 'transform', or 'export'")] string operation = "validate")
    {
        var items = itemIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        if (items.Length == 0)
        {
            return "Error: No item IDs provided. Please provide a comma-separated list.";
        }

        if (items.Length > 100)
        {
            return "Error: Maximum 100 items per batch. Please split into smaller batches.";
        }

        var job = await _jobService.StartOperationAsync(
            operationType: "BatchProcessing",
            inputParameters: new Dictionary<string, object?>
            {
                ["itemIds"] = items,
                ["operation"] = operation,
                ["itemCount"] = items.Length
            },
            operation: async (job, ct) =>
            {
                var results = new List<object>();
                var successCount = 0;
                var failureCount = 0;

                for (int i = 0; i < items.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    var itemId = items[i];
                    var progress = (int)((double)(i + 1) / items.Length * 100);
                    
                    _jobService.UpdateJobStatus(
                        job.JobId, 
                        JobStatus.Running, 
                        $"Processing item {i + 1}/{items.Length}: {itemId}",
                        progress);

                    // Simulate processing each item
                    await Task.Delay(TimeSpan.FromMilliseconds(200), ct);

                    // Simulate some random successes and failures
                    var success = Random.Shared.NextDouble() > 0.1; // 90% success rate
                    
                    if (success)
                    {
                        successCount++;
                        results.Add(new { ItemId = itemId, Status = "Success", Operation = operation });
                    }
                    else
                    {
                        failureCount++;
                        results.Add(new { ItemId = itemId, Status = "Failed", Error = "Simulated random failure" });
                    }
                }

                return new
                {
                    Operation = operation,
                    TotalItems = items.Length,
                    Successful = successCount,
                    Failed = failureCount,
                    SuccessRate = $"{(double)successCount / items.Length * 100:F1}%",
                    Results = results
                };
            });

        return $"""
            ## Batch Processing Started
            
            Processing {items.Length} items with operation: {operation}
            
            | Property | Value |
            |----------|-------|
            | Job ID | `{job.JobId}` |
            | Operation | {operation} |
            | Item Count | {items.Length} |
            
            ### Track Progress
            
            ```
            get_job_status(jobId: "{job.JobId}")
            ```
            
            _Estimated time: {items.Length * 0.2:F0} seconds_
            """;
    }
}
