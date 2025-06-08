// Program.cs
using Dapr.Workflow;
using PipelineWorkflows.Workflows;
using PipelineWorkflows.Activities;
using PipelineWorkflows.Models;
using Dapr;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprWorkflow(options =>
        {
            // Register Workflows
            options.RegisterWorkflow<ProcessSingleRecordWorkflow>();
            options.RegisterWorkflow<ProcessSingleRecordWorkflowWithLoop>();
            
            // Register Activities
            options.RegisterActivity<PostRecordActivity>();
            options.RegisterActivity<UpdateRecordStatusActivity>();
            // options.RegisterActivity<FetchRecordsActivity>(); // If you create a separate activity for fetching
        });

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAPR_GRPC_PORT")))
{
    Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "50001");
}

var app = builder.Build();

if (app.Environment.IsDevelopment()) {app.UseDeveloperExceptionPage();}

// Dapr will send serialized event object vs. being raw CloudEvent`
app.UseCloudEvents();

// needed for Dapr pub/sub routing
app.MapSubscribeHandler();

Console.WriteLine("PROCESS ID: " + Process.GetCurrentProcess().Id);

// Dapr subscription in [Topic] routes orders topic to this route
app.MapPost("/metrics", [Topic("metricspubsub", "metrics")] async (RecordToProcess record) =>
{
    Console.WriteLine($"Subscriber received metric record: {record.Id}, Data: {record.Data}");

    // TODO. Since we are subscribing to a topic which will retry retryable exceptions, we need to handle
    // duplcate workflow executions by id and prevent that exception from being thrown.
    try
    {
        var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
        var workflowId = $"process-record-workflow-{record.Id}";
        var input = new ProcessRecordWorkflowInput(record.Id, record.Data, record.FailCount);
        await workflowClient.ScheduleNewWorkflowAsync(nameof(ProcessSingleRecordWorkflowWithLoop), workflowId, input);

        return Results.Ok(record);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing record {record.Id}: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.Run();