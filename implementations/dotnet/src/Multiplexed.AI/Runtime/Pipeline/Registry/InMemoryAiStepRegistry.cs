using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline
{
    /// <summary>
    /// Resolves AI runtime step instances from an in-memory step registry.
    /// This implementation is suitable for code-first registration scenarios
    /// where step keys are mapped directly to runtime step instances.
    /// </summary>
    public sealed class InMemoryAiStepRegistry : IAiStepRegistry
    {
        private readonly IReadOnlyDictionary<string, IAiStep> _steps;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryAiStepRegistry"/> class.
        /// </summary>
        /// <param name="steps">The registered runtime steps indexed by declarative step key.</param>
        public InMemoryAiStepRegistry(IEnumerable<KeyValuePair<string, IAiStep>> steps)
        {
            ArgumentNullException.ThrowIfNull(steps);

            _steps = steps.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the runtime step instance for the specified declarative step key.
        /// </summary>
        /// <param name="stepKey">The unique declarative step key.</param>
        /// <returns>The matching runtime step instance.</returns>
        public IAiStep Resolve(string stepKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);

            if (!_steps.TryGetValue(stepKey, out var step))
            {
                throw new InvalidOperationException(
                    $"Runtime step '{stepKey}' was not found.");
            }

            return step;
        }
    }
}