using Multiplexed.Abstractions.Core.ExecutionContext;

namespace Multiplexed.Rbac.Core.ExecutionContext
{
    /// <summary>
    /// Creates durable execution context snapshots from live runtime contexts.
    /// </summary>
    public interface IExecutionContextFactory
    {
        /// <summary>
        /// Creates a shollow from the provided runtime execution context.
        /// </summary>
        /// <param name="context">Current runtime execution context.</param>
        /// <returns>Durable execution context .</returns>
        ExecutionContext CreateCopy(ExecutionContext context, string contextKey = "");
        /// <summary>
        /// Creates a snapshot from the provided runtime execution context.
        /// </summary>
        /// <param name="context">Current runtime execution context.</param>
        /// <returns>Durable execution context snapshot.</returns>
        ExecutionContextSnapshot CreateSnapshot(ExecutionContext context);
    }
}