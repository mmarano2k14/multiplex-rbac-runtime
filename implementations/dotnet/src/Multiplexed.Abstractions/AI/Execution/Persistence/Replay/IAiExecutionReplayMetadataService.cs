using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI.Execution.Persistence.Replay
{
    public interface IAiExecutionReplayMetadataService
    {
        Task SaveTerminalFingerprintAsync(
            AiExecutionRecord record,
            AiExecutionState state,
            CancellationToken cancellationToken = default);
    }
}
