using System.Threading;
using Multiplexed.Rbac.Core.ExecutionContext;

namespace Multiplexed.Rbac.Core.Runtime
{
    public sealed class ExecutionContextAccessor : IExecutionContextAccessor
    {
        private static readonly AsyncLocal<ExecutionContext.ExecutionContext?> _current = new();

        public ExecutionContext.ExecutionContext? Current => _current.Value;

        public void Set(ExecutionContext.ExecutionContext context) => _current.Value = context;

        public void Clear() => _current.Value = null;
    }
}