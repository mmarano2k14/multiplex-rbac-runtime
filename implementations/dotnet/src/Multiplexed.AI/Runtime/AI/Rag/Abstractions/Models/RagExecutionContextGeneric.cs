namespace Multiplexed.AI.Runtime.AI.Rag.Abstractions.Models
{
    /// <summary>
    /// Represents a generic RAG execution context that carries a strongly typed
    /// runtime or business snapshot in addition to the stable base execution data.
    ///
    /// PURPOSE:
    /// - Allows RAG providers and retrieval layers to access strongly typed
    ///   execution state when needed.
    /// - Preserves a clean separation between generic orchestration fields and
    ///   typed execution data.
    /// - Aligns with runtime patterns that already use typed snapshots for
    ///   persistence, replay, restore, or execution-state inspection.
    ///
    /// DESIGN:
    /// - Inherits from <see cref="RagExecutionContext"/> so it can still be passed
    ///   through non-generic orchestration surfaces.
    /// - Adds a typed snapshot without forcing all RAG abstractions to become
    ///   generic.
    ///
    /// USAGE:
    /// - Useful for runtime-aware providers such as execution state providers,
    ///   diagnostics providers, or retrieval layers that inspect restored
    ///   workflow state.
    /// - Consumers that do not require typed access can continue using the base
    ///   <see cref="RagExecutionContext"/>.
    /// </summary>
    /// <typeparam name="TContextSnapshot">
    /// The strongly typed snapshot associated with the current execution.
    /// This may represent runtime state, domain state, or any structured
    /// snapshot used by the surrounding workflow engine.
    /// </typeparam>
    public sealed class RagExecutionContext<TContextSnapshot> : RagExecutionContext
    {
        /// <summary>
        /// Gets the strongly typed snapshot associated with the current RAG execution.
        ///
        /// This value is optional because some flows may not require a typed
        /// snapshot even when using the generic context form.
        /// </summary>
        public TContextSnapshot? ContextSnapshot { get; init; }
    }
}