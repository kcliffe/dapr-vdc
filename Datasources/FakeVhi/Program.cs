using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

// TODO. Setup a CRON job to inoke this endpoint every 5 seconds
app.MapGet("/", async (int uniqueIndex) =>
{
    using var client = new DaprClientBuilder().Build();

    // Publish an event/message using Dapr PubSub
    await client.PublishEventAsync("metricspubsub", "metrics", new NewMetric(uniqueIndex.ToString()));
    
    Console.WriteLine($"Published metric for index {uniqueIndex}");
});

app.Run();

public record NewMetric
{
    public string Id { get; }
    public string Data { get; } = "Sample Metric Data";
    public int FailCount { get; } = 0;

    public NewMetric(string uniqueIndex)
    {
        Id = uniqueIndex;
        Data = $"Sample Metric Data for {uniqueIndex}";
    }
}