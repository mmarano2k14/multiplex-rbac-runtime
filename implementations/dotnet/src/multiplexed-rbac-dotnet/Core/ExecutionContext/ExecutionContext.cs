using System.Collections.Generic;

namespace MultiplexedRbac.Core.ExecutionContext
{
    public sealed class ExecutionContext
    {
        public required string ContextKey { get; set; }
        public required string Project { get; set; }
        public required string UserId { get; set; }

        public required string TenantId { get; set; }
        public required string TenantGroupId { get; set; }

        public required string CurrentNamespace { get; set; }
        public required List<NamespaceEntry> Namespaces { get; set; }

        public int InFlightCount { get; set; }
        public int TtlSeconds { get; set; }
    }
}