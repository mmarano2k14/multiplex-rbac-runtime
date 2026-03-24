using Multiplexed.Rbac.Core.ExecutionContext;



namespace Multiplexed.AI.Runtime.Execution
{
    /// <summary>
    /// Responsible for creating a durable AI execution context
    /// from an existing HTTP execution context.
    /// 
    /// This ensures that:
    /// - AI execution is decoupled from the HTTP lifecycle
    /// - a new ContextKey is generated and owned by the AI runtime
    /// </summary>
    public sealed class AiExecutionContextFactory
    {
        /// <summary>
        /// Creates a new AI execution context based on the current HTTP context.
        /// 
        /// IMPORTANT:
        /// - The returned context MUST be seeded into the store to obtain a new ContextKey.
        /// - The original HTTP ContextKey must NEVER be reused.
        /// </summary>
        public Rbac.Core.ExecutionContext.ExecutionContext CreateFrom(Rbac.Core.ExecutionContext.ExecutionContext source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            return new Rbac.Core.ExecutionContext.ExecutionContext
            {
                // Will be assigned by the store during SeedAsync
                ContextKey = string.Empty,
                Project = source.Project,
                TtlSeconds = source.TtlSeconds,
                CurrentNamespace = source.CurrentNamespace,
                TenantGroupId = source.TenantGroupId,
                TenantId = source.TenantId,
                UserId = source.UserId,
                Namespaces = source.Namespaces.ToList(),
                InFlightCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            };
        }
    }
}