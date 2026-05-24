using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multiplexed.Abstractions.AI.Observability.Context;
using Multiplexed.Abstractions.AI.Observability.Ledger;

namespace Multiplexed.AI.Observability.Ledger
{
    /// <summary>
    /// Records important runtime decisions into the configured decision ledger.
    /// </summary>
    /// <remarks>
    /// This recorder converts a runtime correlation context into an append-only
    /// decision ledger entry. The configured write mode controls whether ledger
    /// failures are ignored, logged, or propagated.
    /// </remarks>
    public sealed class DefaultAiDecisionLedgerRecorder : IAiDecisionLedgerRecorder
    {
        private readonly IAiDecisionLedger _ledger;
        private readonly AiDecisionLedgerRecorderOptions _options;
        private readonly ILogger<DefaultAiDecisionLedgerRecorder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiDecisionLedgerRecorder"/> class.
        /// </summary>
        /// <param name="ledger">The decision ledger.</param>
        /// <param name="options">The recorder options.</param>
        /// <param name="logger">The logger.</param>
        public DefaultAiDecisionLedgerRecorder(
            IAiDecisionLedger ledger,
            IOptions<AiDecisionLedgerRecorderOptions> options,
            ILogger<DefaultAiDecisionLedgerRecorder> logger)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task RecordAsync(
            AiRuntimeCorrelationContext context,
            AiDecisionLedgerCategory category,
            string eventType,
            AiDecisionLedgerOutcome outcome,
            string? reason = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(context.ExecutionId);
            ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

            if (_options.WriteMode == AiDecisionLedgerWriteMode.Disabled)
            {
                return;
            }

            var entry = new AiDecisionLedgerEntry
            {
                EntryId = Guid.NewGuid().ToString("N"),
                ExecutionId = context.ExecutionId,
                Sequence = 0,
                Category = category,
                EventType = eventType,
                Outcome = outcome,
                TimestampUtc = DateTimeOffset.UtcNow,
                RunId = context.RunId,
                StepId = context.StepId,
                StepKey = context.StepKey,
                PipelineName = context.PipelineName,
                PipelineVersion = context.PipelineVersion,
                RuntimeInstanceId = context.RuntimeInstanceId,
                WorkerId = context.WorkerId,
                PolicyKey = context.PolicyKey,
                Provider = context.Provider,
                Model = context.Model,
                Operation = context.Operation,
                Reason = reason,
                CorrelationId = context.CorrelationId,
                TraceId = context.TraceId,
                ClaimToken = context.ClaimToken,
                Metadata = metadata
            };

            try
            {
                await _ledger.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (_options.WriteMode == AiDecisionLedgerWriteMode.BestEffort)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to record AI decision ledger entry in best-effort mode. ExecutionId='{ExecutionId}', Category='{Category}', EventType='{EventType}', Outcome='{Outcome}'.",
                    context.ExecutionId,
                    category,
                    eventType,
                    outcome);
            }
        }
    }
}