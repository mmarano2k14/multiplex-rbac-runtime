// File: RagRetrievalAiStep.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.Abstractions.AI.Steps;

namespace Multiplexed.AI.Runtime.AI.Rag.Steps
{
    /// <summary>
    /// Pipeline step that executes a dynamically resolved RAG retrieval operation.
    ///
    /// PURPOSE:
    /// - Acts as the generic DAG entry point for domain-driven RAG retrieval.
    /// - Delegates execution to <see cref="IRagRetrievalStepDispatcher"/>.
    /// - Returns a serializable retrieval payload compatible with downstream RAG steps.
    ///
    /// DESIGN:
    /// - This step contains no business retrieval logic.
    /// - It preserves the real AI execution context object already carried by the step runtime.
    /// - It does not build a generic <c>RagExecutionContext</c> wrapper.
    /// - Persisted RBAC snapshot access remains available separately through the execution record.
    /// </summary>
    [AiStep("rag.retrieval")]
    public sealed class RagRetrievalAiStep : IAiStep
    {
        private readonly IRagRetrievalStepDispatcher _dispatcher;

        public RagRetrievalAiStep(IRagRetrievalStepDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Name => "rag.retrieval";

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var operation = RagStepHelper.GetRequiredOperation(context);
            var inputs = RagStepHelper.GetResolvedInputs(context);

            var config = new RagRetrievalStepConfig
            {
                Operation = operation
            };

            var batch = await _dispatcher.ExecuteAsync(
                context.Execution,
                inputs,
                config,
                cancellationToken).ConfigureAwait(false);

            return AiStepResult.Ok(
                output: $"RAG retrieval operation '{operation}' completed with {batch.Items.Count} item(s).",
                data: RagStepHelper.BuildRetrievalStepResultData(batch, operation));
        }
    }
}