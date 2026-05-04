using System;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Observability;
using Multiplexed.AI.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Default factory for creating step-scoped AI policy engine instances.
    /// </summary>
    public sealed class DefaultAiPolicyEngineFactory : IAiPolicyEngineFactory
    {
        private readonly IAiPolicyRegistry policyRegistry;
        private readonly IAiPolicyEngineRegistry engineRegistry;
        private readonly IAiRuntimeObservability observability;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiPolicyEngineFactory"/> class.
        /// </summary>
        /// <param name="policyRegistry">The policy registry passed to created engines.</param>
        /// <param name="engineRegistry">The registry used to resolve engine implementation types.</param>
        /// <param name="observability">The runtime observability facade.</param>
        public DefaultAiPolicyEngineFactory(
            IAiPolicyRegistry policyRegistry,
            IAiPolicyEngineRegistry engineRegistry,
            IAiRuntimeObservability observability)
        {
            this.policyRegistry = policyRegistry ?? throw new ArgumentNullException(nameof(policyRegistry));
            this.engineRegistry = engineRegistry ?? throw new ArgumentNullException(nameof(engineRegistry));
            this.observability = observability ?? throw new ArgumentNullException(nameof(observability));
        }

        /// <inheritdoc />
        public IAiPolicyEngine Create(
            AiPolicyKind kind,
            AiStepExecutionContext stepContext)
        {
            ArgumentNullException.ThrowIfNull(stepContext);

            var engineType = engineRegistry.Resolve(kind);

            return (IAiPolicyEngine)Activator.CreateInstance(
                engineType,
                policyRegistry,
                stepContext,
                observability)!;
        }

        /// <inheritdoc />
        public TPolicyEngine Create<TPolicyEngine>(
            AiPolicyKind kind,
            AiStepExecutionContext stepContext)
            where TPolicyEngine : class, IAiPolicyEngine
        {
            var engine = Create(kind, stepContext);

            if (engine is not TPolicyEngine typedEngine)
            {
                throw new InvalidOperationException(
                    $"AI policy engine for kind '{kind}' is '{engine.GetType().FullName}' but was requested as '{typeof(TPolicyEngine).FullName}'.");
            }

            return typedEngine;
        }
    }
}