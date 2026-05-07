using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.Abstractions.AI.Execution.Scheduling
{
    /// <summary>
    /// Represents a delegate used to execute a claimed DAG step.
    /// </summary>
    /// <param name="claimedStep">
    /// The claimed DAG step to execute.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The result produced by the executed DAG step.
    /// </returns>
    public delegate Task<AiStepResult> AiStepExecutionDelegate(
        AiClaimedStep claimedStep,
        CancellationToken cancellationToken);
}