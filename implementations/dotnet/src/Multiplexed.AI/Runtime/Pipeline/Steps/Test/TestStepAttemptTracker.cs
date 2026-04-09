using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.Pipeline.Steps.Test
{
    /// <summary>
    /// Tracks the number of real step executions observed by the flaky retry test step.
    /// This is used by distributed retry and convergence tests to prove deterministic execution behavior.
    /// </summary>
    public sealed class TestStepAttemptTracker
    {
        private int _count;

        /// <summary>
        /// Gets the total number of recorded step execution attempts.
        /// </summary>
        public int Count => Volatile.Read(ref _count);

        /// <summary>
        /// Atomically increments the execution attempt count and returns the new value.
        /// </summary>
        public int Increment()
            => Interlocked.Increment(ref _count);
    }

    /// <summary>
    /// Test step that always fails and records each real execution attempt through a shared tracker.
    /// This allows integration tests to validate retry-aware distributed claim behavior and convergence stability.
    /// </summary>
    [AiStep("test-flaky-retry")]
    public sealed class TestFlakyRetryStep : IAiStep
    {
        private readonly TestStepAttemptTracker _tracker;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFlakyRetryStep"/> class.
        /// </summary>
        /// <param name="tracker">The shared attempt tracker.</param>
        public TestFlakyRetryStep(TestStepAttemptTracker tracker)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        /// <inheritdoc />
        public string Key => "test-flaky-retry";

        /// <inheritdoc />
        public string Name => "test-flaky-retry";

        /// <inheritdoc />
        public Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var attempt = _tracker.Increment();

            return Task.FromResult(new AiStepResult
            {
                Success = false,
                Error = $"Simulated retryable failure attempt {attempt}."
            });
        }
    }
}