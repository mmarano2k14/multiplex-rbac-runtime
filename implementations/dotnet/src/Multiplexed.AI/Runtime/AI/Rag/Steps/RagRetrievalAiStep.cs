using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Rag.Models;
using Multiplexed.Abstractions.AI.Rag.Runtime;
using Multiplexed.Abstractions.AI.Rag.Steps;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Runtime.Execution.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

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

            var helper = context.GetHelper();

            var operation = await helper.GetRequiredConfigAsync<string>(
                "operation",
                cancellationToken).ConfigureAwait(false);

            var arguments = await helper.GetArgumentsAsync(
                new[]
                {
                    "provider",
                    "providerKey",
                    "executionMode",
                    "query",
                    "sourceStep",
                    "composer"
                },
                includeReservedVariables: true,
                cancellationToken).ConfigureAwait(false);

            // 🔥 Typed version (current implementation)
            var typedArguments = arguments.BindWithExtras<RagRetrievalArguments>();

            /*
            ======================================================================
            📌 DTO USAGE EXAMPLE (how to read values)
            ======================================================================

            // Access strongly typed properties
            var provider = typedArguments.Provider;
            var providerKey = typedArguments.ProviderKey;
            var executionMode = typedArguments.ExecutionMode;
            var query = typedArguments.Query;

            // Access dynamic inputs stored in AdditionalInputs
            // Example: retrieve candidateId (commonly passed dynamically)
            var candidateId = typedArguments.GetRequiredAdditional<string>("candidateId");

            // Optional value example
            var score = typedArguments.GetAdditional<int?>("score");

            // Safe fallback using raw dictionary access
            if (typedArguments.AdditionalInputs.TryGetValue("jobId", out var jobRaw))
            {
                var jobId = jobRaw?.ToString();
            }

            ======================================================================
            */

            var config = new RagRetrievalStepConfig
            {
                Operation = operation
            };

            var batch = await _dispatcher.ExecuteAsync(
                context.Execution,
                arguments.ToDictionary(),
                config,
                cancellationToken).ConfigureAwait(false);

            /*
            ======================================================================
            🔁 NON-TYPED VERSION (previous implementation for reference)
            ======================================================================

            var inputs = await helper.GetResolvedArgumentsAsync(
                new[]
                {
                    "provider",
                    "providerKey",
                    "executionMode",
                    "query",
                    "sourceStep",
                    "composer"
                },
                includeReservedVariables: true,
                cancellationToken).ConfigureAwait(false);

            var batch = await _dispatcher.ExecuteAsync(
                context.Execution,
                inputs,
                config,
                cancellationToken).ConfigureAwait(false);

            ======================================================================
            KEY DIFFERENCE:
            - Non-typed → Dictionary<string, object?> everywhere ❌
            - Typed → safer access, DTO support, better maintainability ✔
            ======================================================================
            */

            return AiStepResult.Ok(
                output: $"RAG retrieval operation '{operation}' completed with {batch.Items.Count} item(s).",
                data: helper.ToDictionary(new
                {
                    providerKey = operation,
                    argumentProvider = typedArguments.Provider,
                    argumentProviderKey = typedArguments.ProviderKey,
                    argumentExecutionMode = typedArguments.ExecutionMode,
                    itemCount = batch.Items.Count,
                    batch,
                    diagnostics = batch.Diagnostics
                }, ignoreNull: true));
        }
    }
}