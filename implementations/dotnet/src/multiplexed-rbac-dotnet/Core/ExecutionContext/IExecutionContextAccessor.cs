namespace MultiplexedRbac.Core.ExecutionContext
{
    public interface IExecutionContextAccessor
    {
        ExecutionContext? Current { get; }  
        void Set(ExecutionContext context);
        void Clear();                       
    }
}