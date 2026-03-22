using System.Threading;
using ExecutionContext = MultiplexedRbac.Core.ExecutionContext.ExecutionContext;

namespace MultiplexedRbac.Runtime
{
    public sealed class ExecutionContextAccessor : MultiplexedRbac.Core.ExecutionContext.IExecutionContextAccessor
    {
        private static readonly AsyncLocal<ExecutionContext?> _current = new();

        public ExecutionContext? Current => _current.Value;

        public void Set(ExecutionContext context) => _current.Value = context;

        public void Clear() => _current.Value = null;
    }
}