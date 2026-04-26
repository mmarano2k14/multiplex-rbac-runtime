using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;

namespace Multiplexed.AI.Runtime.Execution.Context
{
    /// <summary>
    /// Default factory for creating step-scoped context helpers.
    /// </summary>
    public sealed class DefaultAiStepContextHelperFactory : IAiStepContextHelperFactory
    {
        private readonly IAiContextValueResolver _resolver;

        /// <summary>
        /// Initializes a new instance of the factory.
        /// </summary>
        public DefaultAiStepContextHelperFactory(
            IAiContextValueResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary>
        /// Creates a helper bound to the provided AI step execution context.
        /// </summary>
        public IAiStepContextHelper Create(AiStepExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return new DefaultAiStepContextHelper(
                context,
                _resolver);
        }
    }
}