namespace WriterWorkflow.Models;

public record RecordToProcess(string Id, string Data, int FailCount, string Status);
public record PostApiResult(bool IsSuccess, int StatusCode, string ErrorMessage);
public record ProcessRecordWorkflowInput(string RecordId, string RecordData, int CurrentFailCount);
public record ProcessRecordWorkflowOutput(string RecordId, string FinalStatus);