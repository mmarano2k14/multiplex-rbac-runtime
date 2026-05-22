using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retention;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Retry;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution
{
    /// <summary>
    /// Prints enterprise runtime execution summaries.
    /// </summary>
    public sealed class EnterpriseRuntimeExecutionReporter
    {
        /// <summary>
        /// Prints the execution summary.
        /// </summary>
        /// <param name="handle">
        /// The run handle.
        /// </param>
        /// <param name="final">
        /// The final execution record.
        /// </param>
        public void PrintExecutionSummary(
            AiRuntimeWorkerRunHandle handle,
            AiExecutionRecord final)
        {
            Console.WriteLine("Execution completed");
            Console.WriteLine("-------------------");
            Console.WriteLine($"RunId:       {handle.RunId}");
            Console.WriteLine($"ExecutionId: {handle.ExecutionId}");
            Console.WriteLine($"Status:      {final.Status}");
            Console.WriteLine($"Terminal:    {final.IsTerminal}");
            Console.WriteLine($"Steps:       {final.CompletedSteps.Count}");
            Console.WriteLine();
        }

        /// <summary>
        /// Prints distributed worker metrics.
        /// </summary>
        /// <param name="workerCycles">
        /// The worker cycles by runtime instance.
        /// </param>
        public void PrintWorkerSummary(
            IReadOnlyDictionary<string, long> workerCycles)
        {
            Console.WriteLine("Distributed workers");
            Console.WriteLine("-------------------");

            if (workerCycles.Count == 0)
            {
                Console.WriteLine("No worker metrics were recorded.");
            }
            else
            {
                foreach (var item in workerCycles.OrderBy(
                             item => item.Key,
                             StringComparer.Ordinal))
                {
                    Console.WriteLine($"RuntimeInstanceId: {item.Key} | Cycles: {item.Value}");
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Prints retry recovery summary.
        /// </summary>
        /// <param name="request">
        /// The execution request.
        /// </param>
        /// <param name="retrySummary">
        /// The retry recovery summary.
        /// </param>
        public void PrintRetrySummary(
            EnterpriseRuntimeExecutionRequest request,
            EnterpriseRuntimeRetrySummary retrySummary)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            ArgumentNullException.ThrowIfNull(
                retrySummary);

            if (!request.ExpectRetryRecovery)
            {
                return;
            }

            Console.WriteLine("Retry recovery");
            Console.WriteLine("--------------");

            if (retrySummary.RetryCountsByStepName.Count == 0)
            {
                Console.WriteLine("No expected retried steps were configured.");
                Console.WriteLine();

                return;
            }

            if (retrySummary.RetryCountsByStepName.Count == 1)
            {
                var item = retrySummary.RetryCountsByStepName.First();

                Console.WriteLine($"Step:       {item.Key}");
                Console.WriteLine($"RetryCount: {item.Value}");
                Console.WriteLine();

                return;
            }

            Console.WriteLine($"Expected retried steps: {retrySummary.RetryCountsByStepName.Count}");
            Console.WriteLine($"Retried steps:          {retrySummary.RetriedStepCount}");
            Console.WriteLine($"Minimum retry count:    {retrySummary.MinimumRetryCount}");
            Console.WriteLine($"Maximum retry count:    {retrySummary.MaximumRetryCount}");
            Console.WriteLine($"All retried:            {retrySummary.AllExpectedStepsRetried}");
            Console.WriteLine();
        }

        /// <summary>
        /// Prints validation summary.
        /// </summary>
        public void PrintValidationSummary()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Validated");
            Console.WriteLine("---------");
            Console.ResetColor();
            Console.WriteLine("- Controller execution path");
            Console.WriteLine("- Distributed worker participation");
            Console.WriteLine("- RunId / ExecutionId separation");
            Console.WriteLine("- Retry recovery validation");
            Console.WriteLine("- Terminal Completed status");
            Console.WriteLine("- Persisted execution record before cleanup");
        }

        /// <summary>
        /// Prints terminal retention summary.
        /// </summary>
        /// <param name="retentionSummary">
        /// The terminal retention summary.
        /// </param>
        public void PrintRetentionSummary(
            EnterpriseRuntimeRetentionSummary retentionSummary)
        {
            ArgumentNullException.ThrowIfNull(
                retentionSummary);

            if (!retentionSummary.MaxHotStateStepCount.HasValue)
            {
                return;
            }

            Console.WriteLine("Retention summary");
            Console.WriteLine("-----------------");
            Console.WriteLine($"Configured hot state limit:     {retentionSummary.MaxHotStateStepCount}");
            Console.WriteLine($"Terminal hot state steps:       {retentionSummary.ActualHotStateStepCount}");
            Console.WriteLine($"Steps no longer in hot state:   {retentionSummary.StepsRemovedFromHotState}");
            Console.WriteLine($"Hot state limit respected:      {retentionSummary.HotStateLimitRespected}");
            Console.WriteLine();
        }

        /// <summary>
        /// Prints the demo header.
        /// </summary>
        public static void PrintHeader()
        {
            Console.WriteLine("Enterprise Runtime Demo");
            Console.WriteLine("=======================");
            Console.WriteLine();
        }

        /// <summary>
        /// Writes an error line.
        /// </summary>
        /// <param name="message">
        /// The error message.
        /// </param>
        public static void WriteError(
            string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(
                message);

            Console.ResetColor();
        }
    }
}