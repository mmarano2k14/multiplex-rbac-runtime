namespace Multiplexed.Abstractions.AI.Execution
{
    public interface IAiExecutionKeyBuilder
    {
        string GetExecutionRecordKey(string executionId);
        string GetExecutionStateKey(string executionId);
        string GetDagStepIdsKey(string executionId);
        string GetDagStepKey(string executionId, string stepId);
        string GetDagClaimKey(string executionId, string stepId);
        string GetDagLeaseKey(string executionId, string stepId);
        string GetDagInFlightKey(string executionId);
        string GetDagMetaKey(string executionId);
        string GetDagStepKeyPrefix(string executionId);
    }
}