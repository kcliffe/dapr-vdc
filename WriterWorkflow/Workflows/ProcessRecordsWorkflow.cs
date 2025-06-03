using Dapr.Workflow;
using WriterWorkflow.Models;

namespace WriterWorkflow.Workflows;

public partial class ProcessRecordsWorkflow : Workflow<object, bool> // Input and output might be flexible here
{
    // Assume you have a data service to interact with your database
    // private readonly IRecordDataService _recordDataService;

    // public ProcessRecordsWorkflow(IRecordDataService recordDataService)
    // {
    //     _recordDataService = recordDataService;
    // }

    public ProcessRecordsWorkflow()
    {
    }

    public override async Task<bool> RunAsync(WorkflowContext context, object input)
    {
        var logger = context.CreateReplaySafeLogger<ProcessRecordsWorkflow>();

        LogWorkflowStart(logger);

        // 1. Query records from the database.
        // This activity would fetch records where status is "Created" AND fail_count <= 3
        // You might consider a separate activity for this or do it in the workflow.
        // For simplicity, let's simulate fetching records.
        // In a real scenario, this could be a Dapr binding to your database.

        List<RecordToProcess> recordsToProcess;
        try
        {
            // Simulate fetching records from DB
            // recordsToProcess = await _recordDataService.GetRecordsByStatusAndFailCount("Created", 3);
            recordsToProcess = [.. Enumerable.Range(1, 10).Select(i => new RecordToProcess(
                $"rec_{i}",
                $"data{i}"
            ))];
           
            LogRecordsToProcess(logger, recordsToProcess.Count);
        }
        catch (Exception ex)
        {
            LogFetchError(logger, ex.Message);
            return false; // Or throw, depending on desired error handling
        }

        if (recordsToProcess.Count == 0)
        {
            LogNoRecords(logger);
            return true;
        }

        // 2. Start a child workflow for each record in parallel (Fan-out pattern)
        var tasks = new List<Task<ProcessRecordWorkflowOutput>>();
        foreach (var record in recordsToProcess)
        {
            // Each child workflow gets a unique instance ID (e.g., record.Id)
            // You can also pass the instance ID explicitly if you want to query it later
            string childWorkflowInstanceId = $"{context.InstanceId}-{record.Id}";

            // The child workflow will handle its own retries and fail count management
            tasks.Add(context.CallChildWorkflowAsync<ProcessRecordWorkflowOutput>(
                nameof(ProcessSingleRecordWorkflowWithLoop),            
                new ProcessRecordWorkflowInput(record.Id, record.Data, record.FailCount),
                new ChildWorkflowTaskOptions(childWorkflowInstanceId)));
        }

        // 3. Wait for all child workflows to complete (Fan-in pattern)
        var results = await Task.WhenAll(tasks);

        LogChildWorkflowsCompleted(logger, tasks.Count);

        // You can inspect results here if needed, e.g., for logging
        foreach (var result in results)
        {
            LogChildWorkflowStatus(logger, result.RecordId, result.FinalStatus);
        }

        return true;
    }

    [LoggerMessage(LogLevel.Information, "Starting ProcessRecordsWorkflow to find 'Created' records.")]
    static partial void LogWorkflowStart(ILogger logger);

    [LoggerMessage(LogLevel.Information, "No 'Created' records found to process. Workflow completing.")]
    static partial void LogNoRecords(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Found {count} records to process.")]
    static partial void LogRecordsToProcess(ILogger logger, int count);

    [LoggerMessage(LogLevel.Information,"Failed to fetch records: {error}.")]
    static partial void LogFetchError(ILogger logger, string error);

    [LoggerMessage(LogLevel.Information,"All {numChildWorkflows} child workflows completed.")]
    static partial void LogChildWorkflowsCompleted(ILogger logger, int numChildWorkflows);

     [LoggerMessage(LogLevel.Information,"Child workflow for record {recordId} finished with status: {finalStatus}")]
    static partial void LogChildWorkflowStatus(ILogger logger, string recordId, string finalStatus);

}