using Multiplexed.Abstractions.AI.Pipeline;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Tests.Models
{
    /// <summary>
    /// In-memory step registry used for tests.
    /// </summary>
    public sealed class FakeInMemoryAiStepRegistry : IAiStepRegistry
    {
        private readonly IReadOnlyDictionary<string, IAiStep> _steps;

        public FakeInMemoryAiStepRegistry(IEnumerable<KeyValuePair<string, IAiStep>> steps)
        {
            ArgumentNullException.ThrowIfNull(steps);

            _steps = steps.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        public IAiStep Resolve(string stepKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);

            if (!_steps.TryGetValue(stepKey, out var step))
            {
                throw new InvalidOperationException(
                    $"Step '{stepKey}' was not found.");
            }

            return step;
        }
    }
}