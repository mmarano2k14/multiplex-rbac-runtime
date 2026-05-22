using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Instance.Worker;
using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Scenarios.Chaos;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Replay
{
    /// <summary>
    /// Validates replay consistency for enterprise runtime executions.
    /// </summary>
    public static class EnterpriseRuntimeReplayValidator
    {
        /// <summary>
        /// Creates a deterministic replay fingerprint.
        /// </summary>
        /// <param name="scenario">
        /// The distributed chaos scenario.
        /// </param>
        /// <param name="executionId">
        /// The execution identifier.
        /// </param>
        /// <param name="record">
        /// The execution record.
        /// </param>
        /// <param name="state">
        /// The execution state.
        /// </param>
        /// <param name="resolver">
        /// The execution step resolver.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token.
        /// </param>
        /// <returns>
        /// The replay fingerprint.
        /// </returns>
        public static async Task<EnterpriseRuntimeReplayFingerprint> CreateFingerprintAsync(
            DistributedChaosScenario scenario,
            string executionId,
            AiExecutionRecord record,
            AiExecutionState state,
            IAiExecutionStepResolver resolver,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                scenario);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                executionId);

            ArgumentNullException.ThrowIfNull(
                record);

            ArgumentNullException.ThrowIfNull(
                state);

            ArgumentNullException.ThrowIfNull(
                resolver);

            var selectedSteps = scenario.FullStepFingerprint
                ? scenario.PipelineDefinition.Steps
                : scenario.PipelineDefinition.Steps.Where(step =>
                    !string.IsNullOrWhiteSpace(step.Name) &&
                    scenario.FingerprintStepNames.Contains(
                        step.Name,
                        StringComparer.Ordinal));

            var stepStatuses = new SortedDictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var step in selectedSteps)
            {
                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    continue;
                }

                var status = await resolver.GetStepStatusAsync(
                        executionId,
                        step.Name,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (status is null)
                {
                    throw new InvalidOperationException(
                        $"Unable to resolve status for step '{step.Name}'.");
                }

                stepStatuses[step.Name] = status.Status.ToString();
            }

            var retryCounts = new SortedDictionary<string, int>(
                StringComparer.Ordinal);

            foreach (var stepName in scenario.ExpectedRetriedSteps)
            {
                var step = await resolver.GetStepAsync(
                        executionId,
                        stepName,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (step is null)
                {
                    throw new InvalidOperationException(
                        $"Unable to resolve retried step '{stepName}'.");
                }

                retryCounts[stepName] =
                    step.RetryState?.RetryCount ?? 0;
            }

            var requiredResolvedSteps = new SortedDictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var stepName in scenario.RequiredResolvedSteps)
            {
                var step = await resolver.GetStepAsync(
                        executionId,
                        stepName,
                        state,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (step is null)
                {
                    throw new InvalidOperationException(
                        $"Unable to resolve required step '{stepName}'.");
                }

                requiredResolvedSteps[stepName] =
                    step.Status.ToString();
            }

            return new EnterpriseRuntimeReplayFingerprint
            {
                Status = record.Status.ToString(),
                IsTerminal = record.IsTerminal,
                CompletedSteps = record.CompletedSteps
                    .OrderBy(step => step, StringComparer.Ordinal)
                    .ToArray(),
                StepStatuses = stepStatuses,
                RetryCounts = retryCounts,
                RequiredResolvedSteps = requiredResolvedSteps
            };
        }

        /// <summary>
        /// Validates that two replay fingerprints are equivalent.
        /// </summary>
        /// <param name="beforeReplay">
        /// The fingerprint captured before replay.
        /// </param>
        /// <param name="afterReplay">
        /// The fingerprint captured after replay.
        /// </param>
        public static void ValidateMatch(
            EnterpriseRuntimeReplayFingerprint beforeReplay,
            EnterpriseRuntimeReplayFingerprint afterReplay)
        {
            ArgumentNullException.ThrowIfNull(
                beforeReplay);

            ArgumentNullException.ThrowIfNull(
                afterReplay);

            if (!string.Equals(
                    beforeReplay.Status,
                    afterReplay.Status,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Replay status mismatch. Before='{beforeReplay.Status}', After='{afterReplay.Status}'.");
            }

            if (beforeReplay.IsTerminal != afterReplay.IsTerminal)
            {
                throw new InvalidOperationException(
                    $"Replay terminal flag mismatch. Before='{beforeReplay.IsTerminal}', After='{afterReplay.IsTerminal}'.");
            }

            ValidateSequence(
                "completed steps",
                beforeReplay.CompletedSteps,
                afterReplay.CompletedSteps);

            ValidateDictionary(
                "step statuses",
                beforeReplay.StepStatuses,
                afterReplay.StepStatuses);

            ValidateDictionary(
                "retry counts",
                beforeReplay.RetryCounts,
                afterReplay.RetryCounts);

            ValidateDictionary(
                "required resolved steps",
                beforeReplay.RequiredResolvedSteps,
                afterReplay.RequiredResolvedSteps);
        }

        /// <summary>
        /// Validates that two sequences are equivalent.
        /// </summary>
        /// <param name="name">
        /// The sequence name.
        /// </param>
        /// <param name="expected">
        /// The expected sequence.
        /// </param>
        /// <param name="actual">
        /// The actual sequence.
        /// </param>
        private static void ValidateSequence(
            string name,
            IReadOnlyList<string> expected,
            IReadOnlyList<string> actual)
        {
            if (expected.Count != actual.Count)
            {
                throw new InvalidOperationException(
                    $"Replay {name} count mismatch. Expected='{expected.Count}', Actual='{actual.Count}'.");
            }

            for (var index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(
                        expected[index],
                        actual[index],
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Replay {name} mismatch at index '{index}'. Expected='{expected[index]}', Actual='{actual[index]}'.");
                }
            }
        }

        /// <summary>
        /// Validates that two dictionaries are equivalent.
        /// </summary>
        /// <typeparam name="TValue">
        /// The dictionary value type.
        /// </typeparam>
        /// <param name="name">
        /// The dictionary name.
        /// </param>
        /// <param name="expected">
        /// The expected dictionary.
        /// </param>
        /// <param name="actual">
        /// The actual dictionary.
        /// </param>
        private static void ValidateDictionary<TValue>(
            string name,
            IReadOnlyDictionary<string, TValue> expected,
            IReadOnlyDictionary<string, TValue> actual)
        {
            if (expected.Count != actual.Count)
            {
                throw new InvalidOperationException(
                    $"Replay {name} count mismatch. Expected='{expected.Count}', Actual='{actual.Count}'.");
            }

            foreach (var item in expected)
            {
                if (!actual.TryGetValue(item.Key, out var actualValue))
                {
                    throw new InvalidOperationException(
                        $"Replay {name} missing key '{item.Key}'.");
                }

                if (!EqualityComparer<TValue>.Default.Equals(
                        item.Value,
                        actualValue))
                {
                    throw new InvalidOperationException(
                        $"Replay {name} mismatch for key '{item.Key}'. Expected='{item.Value}', Actual='{actualValue}'.");
                }
            }
        }
    }
}