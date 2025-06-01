// Program.cs
using Dapr.Workflow;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WriterWorkflow.Workflows;
using WriterWorkflow.Activities;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(new HttpClient()); // Or use IHttpClientFactory
        services.AddDaprWorkflow(options =>
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
    })
    .Build();

host.Run();