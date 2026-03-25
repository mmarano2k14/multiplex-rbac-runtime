using Multiplexed.Abstractions.Core.ExecutionContext;
using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

public class FakeExecutionContextFactory : IExecutionContextFactory
{
    public ExecutionContext CreateCopy(ExecutionContext context, string contextKey = "")
    {
        return new ExecutionContext
        {
            ContextKey = contextKey,
            Project = context.Project,
            TenantId = context.TenantId,
            TenantGroupId = context.TenantGroupId,
            CurrentNamespace = context.CurrentNamespace,
            UserId = context.UserId,
            Namespaces = context.Namespaces,
            TtlSeconds = context.TtlSeconds
        };
    }

    public ExecutionContextSnapshot CreateSnapshot(ExecutionContext context)
    {
        return new ExecutionContextSnapshot
        {
            ContextKey = context.ContextKey,
            TenantId = context.TenantId,
            TenantGroupId = context.TenantGroupId,
            UserId = context.UserId,
            Project = context.Project,
            CurrentNamespace = context.CurrentNamespace,
            Namespaces = context.Namespaces?.ToList() ?? new List<NamespaceEntry>(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
        
}