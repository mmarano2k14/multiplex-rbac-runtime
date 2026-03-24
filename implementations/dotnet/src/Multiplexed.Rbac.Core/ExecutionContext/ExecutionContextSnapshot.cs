using System;
using System.Collections.Generic;

namespace Multiplexed.Rbac.Core.ExecutionContext
{
    /// <summary>
    /// Represents a durable snapshot of an execution context.
    /// This snapshot is intended to be persisted and later used to
    /// reconstruct a runtime execution context outside the original HTTP request.
    /// </summary>
    public sealed class ExecutionContextSnapshot
    {
        /// <summary>
        /// Original context key at snapshot creation time.
        /// This is stored for traceability only and should not be reused
        /// as the durable execution key for AI orchestration.
        /// </summary>
        public required string ContextKey { get; set; }
        public required string Project { get; set; }
        public required string UserId { get; set; }

        public required string TenantId { get; set; }
        public required string TenantGroupId { get; set; }

        public required string CurrentNamespace { get; set; }
        public required List<NamespaceEntry> Namespaces { get; set; }
        public int InFlightCount { get; set; }
        public int TtlSeconds { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}