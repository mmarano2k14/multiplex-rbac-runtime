using System;

namespace Multiplexed.AI.Runtime.Logging
{
    /// <summary>
    /// Default implementation of <see cref="IAiRagLogger"/>.
    ///
    /// PURPOSE:
    /// - Acts as the root logging gateway for the RAG subsystem.
    /// - Exposes specialized loggers for retrieval and composition concerns.
    ///
    /// DESIGN:
    /// - Keeps retrieval and composition logging responsibilities separated.
    /// - Provides a single dependency to inject into RAG runtime services.
    /// </summary>
    public sealed class AiRagLogger : IAiRagLogger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiRagLogger"/> class.
        /// </summary>
        /// <param name="retrieval">
        /// The logger dedicated to retrieval orchestration events.
        /// </param>
        /// <param name="composition">
        /// The logger dedicated to composition execution events.
        /// </param>
        public AiRagLogger(
            IAiRagRetrievalLogger retrieval,
            IAiRagCompositionLogger composition)
        {
            Retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        }

        /// <inheritdoc />
        public IAiRagRetrievalLogger Retrieval { get; }

        /// <inheritdoc />
        public IAiRagCompositionLogger Composition { get; }
    }
}