// Program.cs
using Dapr.Workflow;
using WriterWorkflow.Workflows;
using WriterWorkflow.Activities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprWorkflow(options =>
        {
            // Register Workflows
            options.RegisterWorkflow<ProcessRecordsWorkflow>();
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
app.Run();