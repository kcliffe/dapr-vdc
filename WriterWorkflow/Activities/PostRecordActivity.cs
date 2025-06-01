using Dapr.Workflow;
using System.Net;
using WriterWorkflow.Models;
using System.Text.Json;
using System.Text;

namespace WriterWorkflow.Activities;

public class PostRecordActivity : WorkflowActivity<RecordToProcess, PostApiResult>
{
    private readonly HttpClient _httpClient; // Inject your HttpClient
    private readonly ILogger<PostRecordActivity> _logger;

    public PostRecordActivity(HttpClient httpClient, ILogger<PostRecordActivity> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public override async Task<PostApiResult> RunAsync(WorkflowActivityContext context, RecordToProcess record)
    {
        _logger.LogInformation("Attempting to post record {recordId}...", record.Id);

        try
        {
            // Simulate API call
            var content = new StringContent(JsonSerializer.Serialize(record.Data), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync("https://external-api.com/records", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted record {recordId}.", record.Id);
                return new PostApiResult(true, (int)response.StatusCode, string.Empty);
            }
            else if (response.StatusCode == HttpStatusCode.UnprocessableEntity) // 422
            {
                _logger.LogWarning("API returned 422 for record {recordId}: {response}", record.Id, await response.Content.ReadAsStringAsync());
                return new PostApiResult(false, (int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            else // Consider other non-success codes as transient for workflow retry
            {
                _logger.LogError("API returned transient error {statusCode} for record {recordId}: {response}", response.StatusCode, record.Id, await response.Content.ReadAsStringAsync());
                // Dapr Workflow retry policies will handle this by re-throwing
                throw new HttpRequestException($"API call failed with status code: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Network or API error for record {recordId}: {message}", record.Id, ex.Message);
            // Re-throw to trigger Dapr Workflow retry policy
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error in PostRecordActivity for record {recordId}: {message}", record.Id, ex.Message);
            throw;
        }
    }
}