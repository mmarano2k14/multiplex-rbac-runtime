using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI.Execution.Cleanup
{
    public interface IAiExecutionSnapshotCleanupService
    {
        Task<AiExecutionSnapshotCleanupResult> DeleteSnapshotAsync(
            string executionId,
            CancellationToken cancellationToken = default);
    }
}
