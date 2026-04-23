using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Abstractions.AI.Execution.Cleanup
{
    public sealed class AiExecutionSnapshotCleanupResult
    {
        public bool SnapshotFound { get; set; }

        public bool SnapshotDeleted { get; set; }

        public List<string> Warnings { get; set; } = new();

        public List<string> Errors { get; set; } = new();
    }
}
