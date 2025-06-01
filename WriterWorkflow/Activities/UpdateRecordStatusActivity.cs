using Dapr.Workflow;
using Microsoft.Extensions.Logging;
using WriterWorkflow.Models;

namespace WriterWorkflow.Activities;

public class UpdateRecordStatusActivity : WorkflowActivity<RecordToProcess, bool>
{
    // Assume you have a data service to interact with your database
    // private readonly IRecordDataService _recordDataService;
    private readonly ILogger<UpdateRecordStatusActivity> _logger;

    public UpdateRecordStatusActivity(ILogger<UpdateRecordStatusActivity> logger)
    {
        _logger = logger;
    }

    public override async Task<bool> RunAsync(WorkflowActivityContext context, RecordToProcess record)
    {
        _logger.LogInformation($"Updating status for record {record.Id} to '{record.Status}', FailCount: {record.FailCount}");

        // Simulate database update
        // await _recordDataService.UpdateRecordStatus(record.Id, record.Status, record.FailCount);

        return true; // Indicate success of update
    }
}