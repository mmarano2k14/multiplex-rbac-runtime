using System;

namespace Multiplexed.AI.Runtime.Metrics.Resolvers
{
    /// <summary>
    /// Records metrics for runtime resolver operations.
    ///
    /// PURPOSE:
    /// - Observe input binding and path resolution behavior.
    /// - Track successful resolutions, misses, and failures.
    /// - Help diagnose invalid pipeline bindings or missing runtime data.
    ///
    /// RESOLVER LAYER:
    /// - Resolves paths such as state.*, steps.* or provider-specific bindings.
    /// - Converts declared pipeline inputs into runtime values.
    ///
    /// IMPORTANT:
    /// - This interface is observational only.
    /// - It must not resolve values or modify execution state.
    /// </summary>
    public interface IAiResolverMetrics
    {
        /// <summary>
        /// Records that a resolver operation started.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="path">The path or binding being resolved.</param>
        void RecordResolveStarted(string executionId, string stepId, string path);

        /// <summary>
        /// Records that a resolver operation completed successfully.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="path">The path or binding that was resolved.</param>
        void RecordResolveSuccess(string executionId, string stepId, string path);

        /// <summary>
        /// Records that a resolver operation completed but did not find a value.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="path">The path or binding that could not be resolved.</param>
        void RecordResolveMiss(string executionId, string stepId, string path);

        /// <summary>
        /// Records that a resolver operation failed.
        /// </summary>
        /// <param name="executionId">The execution identifier.</param>
        /// <param name="stepId">The step identifier.</param>
        /// <param name="path">The path or binding being resolved.</param>
        /// <param name="exception">The exception that occurred.</param>
        void RecordResolveFailed(string executionId, string stepId, string path, Exception exception);
    }
}