namespace Multiplexed.AI.Runtime.Plugins
{
    /// <summary>
    /// Creates strongly typed plugin execution context instances at runtime.
    ///
    /// WHY THIS EXISTS:
    /// - The runtime only knows the expected execution context type dynamically
    /// - External operations expect IPluginExecutionContext&lt;TContextSnapshot&gt;
    /// - This factory centralizes validation + typed context creation
    /// </summary>
    public interface IPluginExecutionContextFactory
    {
        /// <summary>
        /// Creates a plugin execution context matching the expected runtime type.
        /// </summary>
        /// <param name="executionContextType">
        /// The expected execution context CLR type.
        /// </param>
        /// <param name="executionContext">
        /// The runtime execution context instance.
        /// </param>
        /// <param name="inputs">
        /// Resolved inputs for the current step execution.
        /// </param>
        /// <returns>
        /// A boxed IPluginExecutionContext&lt;TContextSnapshot&gt; instance.
        /// </returns>
        object Create(
            Type executionContextType,
            object executionContext,
            IReadOnlyDictionary<string, object?> inputs);
    }
}