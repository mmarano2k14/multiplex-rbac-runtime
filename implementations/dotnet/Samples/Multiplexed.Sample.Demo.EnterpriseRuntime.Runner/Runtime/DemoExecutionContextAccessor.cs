using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime
{
    /// <summary>
    /// Provides an in-memory execution context for the enterprise runtime demo runner.
    ///
    /// This class intentionally lives inside the demo runner project so the runner does
    /// not depend on test fixtures or test-only fake implementations.
    /// </summary>
    public sealed class DemoExecutionContextAccessor : IExecutionContextAccessor
    {
        /// <inheritdoc />
        public ExecutionContext? Current { get; private set; }

        /// <summary>
        /// Sets the current demo execution context.
        /// </summary>
        /// <param name="context">The execution context to expose to runtime services.</param>
        public void Set(ExecutionContext context)
        {
            Current = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Clears the current demo execution context.
        /// </summary>
        public void Clear()
        {
            Current = null;
        }
    }
}