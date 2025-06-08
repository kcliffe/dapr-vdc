namespace PipelineWorkflows.Models;

public record RecordToProcess(string Id, string Data, int FailCount = 0, string Status = "Created");
public record PostApiResult(bool IsSuccess, int StatusCode, string ErrorMessage);
public record ProcessRecordWorkflowInput(string RecordId, string RecordData, int CurrentFailCount);
public record ProcessRecordWorkflowOutput(string RecordId, string FinalStatus);