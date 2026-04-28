using Multiplexed.Abstractions.AI.Execution.Retention.Models;
using Multiplexed.Abstractions.AI.Execution.Retention.Triggers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Tests.Models
{
    public sealed class TestExecutionRetentionTrigger : IAiExecutionRetentionTrigger
    {
        private readonly bool _shouldRun;

        public TestExecutionRetentionTrigger(bool shouldRun)
        {
            _shouldRun = shouldRun;
        }

        public bool ShouldRun(AiExecutionRetentionTriggerContext context)
        {
            return _shouldRun;
        }
    }
}
