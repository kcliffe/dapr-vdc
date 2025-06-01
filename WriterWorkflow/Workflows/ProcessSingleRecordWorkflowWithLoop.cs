using Dapr.Workflow;
using System.Net;
using Microsoft.Extensions.Logging;
using WriterWorkflow.Models;
using WriterWorkflow.Activities;
using WriterWorkflow.Models;

namespace WriterWorkflow.Workflows;

public class ProcessSingleRecordWorkflowWithLoop : Workflow<ProcessRecordWorkflowInput, ProcessRecordWorkflowOutput>
{
    private const int MaxFailCount = 3; // Your existing policy

    public override async Task<ProcessRecordWorkflowOutput> RunAsync(WorkflowContext context, ProcessRecordWorkflowInput input)
    {
        var logger = context.CreateReplaySafeLogger<ProcessSingleRecordWorkflow>();

        string recordId = input.RecordId;
        string recordData = input.RecordData; // Keep recordData accessible
        int currentFailCount = input.CurrentFailCount; // Initial fail count
        string currentStatus = "Created"; // Initial status

        // Loop to handle multiple non-transient retry attempts
        while (currentFailCount <= MaxFailCount && currentStatus == "Created")
        {
            logger.LogInformation("Attempting to process record {recordId} (Attempt {currentFailCountPlusOne}). Current FailCount: {currentFailCount}",
            recordId, currentFailCount + 1, currentFailCount);

            // Define a retry policy for transient errors (e.g., 503) within THIS API CALL
            // This policy is for immediate retries of the *same* API call attempt.
            var transientRetryPolicy = new WorkflowRetryPolicy(
                firstRetryInterval: TimeSpan.FromSeconds(5),
                backoffCoefficient: 2.0,
                maxRetryInterval: TimeSpan.FromMinutes(5),
                maxNumberOfAttempts: 10
            );

            PostApiResult apiResult;
            try
            {
                apiResult = await context.CallActivityAsync<PostApiResult>(
                    nameof(PostRecordActivity),
                    new RecordToProcess(recordId, recordData, currentFailCount, currentStatus),
                    new WorkflowTaskOptions { RetryPolicy = transientRetryPolicy });
            
                if (apiResult.IsSuccess)
                {
                    logger.LogInformation("Record {recordId} successfully processed. Setting status to 'Processed'.", recordId);
                    currentStatus = "Processed";
                    currentFailCount = 0; // Reset fail count on success
                    break; // Exit the loop
                }
                else if (apiResult.StatusCode == (int)HttpStatusCode.UnprocessableEntity) // 422
                {
                    logger.LogWarning("Record {recordId} is invalid (422). Setting status to 'Invalid'.", recordId);
                    currentStatus = "Invalid";
                    currentFailCount = 0; // Reset fail count as it's not a retryable error
                    break; // Exit the loop
                }
            }
            catch (Exception ex)
            {
                // This catch block means the PostRecordActivity failed *after* exhausting its transient retries.
                // This indicates a persistent transient failure, or an unhandled exception.
                logger.LogError("PostRecordActivity failed after transient retries for record {recordId}: {message}", recordId, ex.Message);
                currentFailCount++; // Increment fail count for non-transient/persistent failure
                currentStatus = "Created"; // Keep status as "Created" for potential future retry

                // If fail count is within limits, schedule a delayed retry.
                if (currentFailCount <= MaxFailCount)
                {
                    TimeSpan retryDelay = CalculateRetryDelay(currentFailCount); // Implement your delay logic
                    logger.LogWarning("Scheduling next attempt for record {recordId} in {retryDelayTotalSeconds} seconds (FailCount: {currentFailCount}).",
                        recordId, retryDelay.TotalSeconds, currentFailCount);

                    // Update the record's fail_count in the DB before pausing
                    await context.CallActivityAsync<bool>(
                        nameof(UpdateRecordStatusActivity),
                        new RecordToProcess(recordId, recordData, currentFailCount, currentStatus));

                    // Schedule a timer for the next retry attempt
                    await context.CreateTimer(retryDelay);
                    continue; // Loop again to try the API call after the timer
                }
                else
                {
                    // Fail count exceeded, mark as permanently failed.
                    logger.LogError("Record {recordId} failed after {maxFailCount} retries. Marking as 'PermanentlyFailed'.",
                        recordId, MaxFailCount);
                    currentStatus = "PermanentlyFailed";
                    break; // Exit the loop
                }
            }                      
        } // End while loop

        // Final update to the database with the determined status and fail count
        await context.CallActivityAsync<bool>(
            nameof(UpdateRecordStatusActivity),
            new RecordToProcess(recordId, recordData, currentFailCount, currentStatus));

        return new ProcessRecordWorkflowOutput(recordId, currentStatus);
    }

    // Helper method to calculate exponential delay for non-transient retries
    private static TimeSpan CalculateRetryDelay(int currentFailCount)
    {
        // Example: 1st retry: 30s, 2nd: 60s, 3rd: 120s
        if (currentFailCount == 1) return TimeSpan.FromMinutes(10);
        if (currentFailCount == 2) return TimeSpan.FromMinutes(20);
        if (currentFailCount == 3) return TimeSpan.FromMinutes(30);
        return TimeSpan.FromMinutes(5); // Default for safety, should not be hit if MaxFailCount is 3
    }
}