namespace Multiplexed.AI.Runtime.Plugins
{
    /// <summary>
    /// Default implementation of <see cref="IPluginExecutionContextFactory"/>.
    /// </summary>
    public sealed class PluginExecutionContextFactory : IPluginExecutionContextFactory
    {
        /// <inheritdoc />
        public object Create(
            Type executionContextType,
            object executionContext,
            IReadOnlyDictionary<string, object?> inputs)
        {
            if (executionContextType == null)
            {
                throw new ArgumentNullException(nameof(executionContextType));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (!executionContextType.IsInstanceOfType(executionContext))
            {
                throw new InvalidOperationException(
                    $"Invalid execution context type. Expected '{executionContextType.FullName}', " +
                    $"but received '{executionContext.GetType().FullName}'.");
            }

            var closedType = typeof(PluginExecutionContext<>).MakeGenericType(executionContextType);

            return Activator.CreateInstance(
                closedType,
                executionContext,
                inputs)!;
        }
    }
}