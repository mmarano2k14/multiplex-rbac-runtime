using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.AI.Abstractions.AI.Policies;
using Multiplexed.AI.Runtime.Execution.Context;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Provides shared behavior for step-scoped AI policy engines.
    /// </summary>
    /// <remarks>
    /// This base class is responsible for resolving step-scoped policy configuration,
    /// resolving registered policies, and executing those policies. It does not own
    /// domain-specific decisions such as retry, retention, eviction, or recovery.
    /// </remarks>
    public abstract class AiPolicyEngine : IAiPolicyEngine
    {
        private readonly IAiPolicyRegistry policyRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="AiPolicyEngine"/> class.
        /// </summary>
        /// <param name="policyRegistry">The registry used to resolve policies.</param>
        /// <param name="stepContext">The step execution context bound to this engine instance.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="policyRegistry"/> or <paramref name="stepContext"/> is <see langword="null"/>.
        /// </exception>
        protected AiPolicyEngine(
            IAiPolicyRegistry policyRegistry,
            AiStepExecutionContext stepContext)
        {
            this.policyRegistry = policyRegistry ?? throw new ArgumentNullException(nameof(policyRegistry));
            StepContext = stepContext ?? throw new ArgumentNullException(nameof(stepContext));
        }

        /// <inheritdoc />
        public abstract AiPolicyKind Kind { get; }

        /// <inheritdoc />
        public AiStepExecutionContext StepContext { get; }

        /// <summary>
        /// Resolves a typed policy definition from the current step configuration.
        /// </summary>
        /// <typeparam name="TDefinition">The policy definition type.</typeparam>
        /// <param name="configKey">The step configuration key.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The resolved policy definition, or <see langword="null"/> when missing.</returns>
        protected async Task<TDefinition?> ResolvePolicyDefinitionAsync<TDefinition>(
            string configKey,
            CancellationToken cancellationToken = default)
            where TDefinition : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configKey);

            return await StepContext
                .GetHelper()
                .GetConfigAsync<TDefinition>(configKey, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves policies from ordered policy keys and an expected policy kind.
        /// </summary>
        /// <param name="policyKeys">The ordered policy keys.</param>
        /// <param name="policyKind">The expected policy kind.</param>
        /// <returns>The resolved policies.</returns>
        protected IReadOnlyCollection<IAiPolicy> ResolvePolicies(
            IEnumerable<string> policyKeys,
            AiPolicyKind policyKind)
        {
            ArgumentNullException.ThrowIfNull(policyKeys);

            return policyRegistry.ResolveMany(policyKeys, policyKind);
        }

        /// <summary>
        /// Executes the specified policies against the provided policy context.
        /// </summary>
        /// <typeparam name="TPolicyContext">The policy context type.</typeparam>
        /// <param name="policyContext">The context evaluated by the policies.</param>
        /// <param name="policies">The policies to execute.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The ordered policy results.</returns>
        protected static async Task<IReadOnlyCollection<AiPolicyResult>> ExecutePoliciesAsync<TPolicyContext>(
            TPolicyContext policyContext,
            IReadOnlyCollection<IAiPolicy> policies,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(policyContext);
            ArgumentNullException.ThrowIfNull(policies);

            if (policies.Count == 0)
            {
                return Array.Empty<AiPolicyResult>();
            }

            var results = new List<AiPolicyResult>(policies.Count);

            foreach (var policy in policies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await policy
                    .ExecuteAsync(policyContext, cancellationToken)
                    .ConfigureAwait(false);

                results.Add(result);
            }

            return results;
        }
    }
}