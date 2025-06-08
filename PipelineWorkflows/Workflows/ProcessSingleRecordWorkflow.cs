using Dapr.Workflow;
using PipelineWorkflows.Activities;
using PipelineWorkflows.Models;
using writer_workflow;

namespace PipelineWorkflows.Workflows;

public class ProcessSingleRecordWorkflow : Workflow<ProcessRecordWorkflowInput, ProcessRecordWorkflowOutput>
{
    private const int MaxFailCount = 3; // Your existing policy

    public ProcessSingleRecordWorkflow()
    {
    }

    public override async Task<ProcessRecordWorkflowOutput> RunAsync(WorkflowContext context, ProcessRecordWorkflowInput input)
    {
        var logger = context.CreateReplaySafeLogger<ProcessSingleRecordWorkflow>();
        
        string recordId = input.RecordId;
        string currentStatus = "Created"; // Assume initial status is 'Created'
        int currentFailCount = input.CurrentFailCount;

        logger.LogInformation("Processing record {recordId} in child workflow. Current FailCount: {currentFailCount}", recordId, currentFailCount);

        if (currentFailCount > MaxFailCount)
        {
            logger.LogWarning("Record {recordId} fail count ({currentFailCount}) exceeded max {MaxFailCount}. Skipping processing.", recordId, currentFailCount, MaxFailCount);
            // Optionally, you might want to update status to "PermanentlyFailed" here
            await context.CallActivityAsync<bool>(
                nameof(UpdateRecordStatusActivity),
                new RecordToProcess(recordId, input.RecordData, currentFailCount, "PermanentlyFailed"));
            return new ProcessRecordWorkflowOutput(recordId, "PermanentlyFailed");
        }

        // Define a retry policy for transient errors (e.g., 503)
        var retryPolicy = new WorkflowRetryPolicy(
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0, // Exponential backoff
            maxRetryInterval: TimeSpan.FromMinutes(5), // Cap the max retry interval
            maxNumberOfAttempts: 10 // Maximum attempts before considering it a non-transient failure for this activity
        );

        PostApiResult apiResult;
        try
        {
            apiResult = await context.CallActivityAsync<PostApiResult>(
                nameof(PostRecordActivity),
                new RecordToProcess(recordId, input.RecordData, currentFailCount, currentStatus), // Pass current data
                new WorkflowTaskOptions { RetryPolicy = retryPolicy });

            if (apiResult.IsSuccess)
            {
                logger.LogInformation("Record {recordId} successfully processed. Setting status to 'Processed'.", recordId);

                currentStatus = "Processed";
                await context.CallActivityAsync<bool>(
                    nameof(UpdateRecordStatusActivity),
                    new RecordToProcess(recordId, input.RecordData, 0, currentStatus)); // Reset fail count on success
                return new ProcessRecordWorkflowOutput(recordId, currentStatus);
            }
            
            // only other scenario is 422 Unprocessable Entity
            logger.LogWarning("Record {recordId} is invalid (422). Setting status to 'Invalid'.", recordId);

            currentStatus = Consts.RecordStatuses.Invalid;
            await context.CallActivityAsync<bool>(
                nameof(UpdateRecordStatusActivity),
                new RecordToProcess(recordId, input.RecordData, 0, currentStatus)); // Reset fail count as it's not a retryable error
                
            return new ProcessRecordWorkflowOutput(recordId, currentStatus);                
        }
        catch (Exception ex)
        {
            // If the activity exhausts its retries, or throws an unhandled exception, it lands here.
            // This indicates a persistent transient failure (e.g., external API is down indefinitely).
            logger.LogError("PostRecordActivity failed after retries for record {recordId}: {message}", recordId, ex.Message);

            currentFailCount++;
            currentStatus = "Created"; // Still "Created" as it's meant to be retried by the main process
            await context.CallActivityAsync<bool>(
                nameof(UpdateRecordStatusActivity),
                new RecordToProcess(recordId, input.RecordData, currentFailCount, currentStatus));

            // If fail count is still <= 3, the parent workflow will pick it up on next run.
            // If it's now > 3, the parent workflow will handle it by marking as "PermanentlyFailed".
            return new ProcessRecordWorkflowOutput(recordId, currentStatus);
        }
    }
}