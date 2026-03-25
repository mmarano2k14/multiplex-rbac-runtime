using Multiplexed.Rbac.Core.ExecutionContext;
using ExecutionContext = Multiplexed.Rbac.Core.ExecutionContext.ExecutionContext;

public class InMemoryContextAccessor : IExecutionContextAccessor
{
    public ExecutionContext? Current { get; private set; }

    public void Set(ExecutionContext context) => Current = context;

    public void Clear() => Current = null;
}