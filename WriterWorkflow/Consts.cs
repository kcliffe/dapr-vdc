namespace writer_workflow;

public class Consts
{
    public class WorkflowNames
    {
        public const string ProcessSingleRecordWorkflow = "ProcessSingleRecordWorkflow";
        public const string ProcessRecordsWorkflow = "ProcessRecordsWorkflow";
    }

    public class ActivityNames
    {
        public const string PostRecordActivity = "PostRecordActivity";
        public const string UpdateRecordStatusActivity = "UpdateRecordStatusActivity";
    }

    public class RecordStatuses
    {
        public const string Created = "Created";
        public const string PermanentlyFailed = "PermanentlyFailed";
        public const string Processed = "Processed";
        public const string Invalid = "Invalid";
    }
}
