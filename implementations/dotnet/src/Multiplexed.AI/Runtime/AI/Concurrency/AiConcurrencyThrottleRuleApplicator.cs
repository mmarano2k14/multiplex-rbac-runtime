using Multiplexed.Abstractions.AI.Concurrency;

namespace Multiplexed.AI.Runtime.AI.Concurrency
{
    /// <summary>
    /// Applies generic concurrency throttle rules to a resolved concurrency definition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generic throttle rules are resolved from policy configuration such as:
    /// </para>
    ///
    /// <code>
    /// {
    ///   "name": "concurrency.throttle",
    ///   "config": {
    ///     "scope": "provider",
    ///     "target": "openai",
    ///     "limit": 10
    ///   }
    /// }
    /// </code>
    ///
    /// <para>
    /// Rules are applied after <see cref="AiConcurrencyContext"/> creation because targeted
    /// rules must be matched against runtime context values such as provider, model,
    /// operation, step name, step key, or pipeline key.
    /// </para>
    ///
    /// <para>
    /// Direct concurrency values remain authoritative. Rules only fill missing limits.
    /// </para>
    /// </remarks>
    public static class AiConcurrencyThrottleRuleApplicator
    {
        /// <summary>
        /// Applies matching throttle rules to a concurrency definition.
        /// </summary>
        /// <param name="definition">
        /// The resolved concurrency definition.
        /// </param>
        /// <param name="context">
        /// The runtime concurrency context.
        /// </param>
        /// <returns>
        /// A new effective concurrency definition with matching throttle rules applied.
        /// </returns>
        public static AiConcurrencyDefinition Apply(
            AiConcurrencyDefinition definition,
            AiConcurrencyContext context)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(context);

            if (definition.ThrottleRules.Count == 0)
            {
                return definition;
            }

            var effective = Clone(definition);

            foreach (var rule in definition.ThrottleRules)
            {
                if (!IsValid(rule) || !Matches(rule, context))
                {
                    continue;
                }

                ApplyRule(
                    effective,
                    rule);
            }

            return effective;
        }

        /// <summary>
        /// Applies a matching throttle rule to the effective definition.
        /// </summary>
        /// <param name="definition">
        /// The effective definition being updated.
        /// </param>
        /// <param name="rule">
        /// The matching throttle rule.
        /// </param>
        private static void ApplyRule(
            AiConcurrencyDefinition definition,
            AiConcurrencyThrottleRule rule)
        {
            var scope = Normalize(rule.Scope);

            switch (scope)
            {
                case "provider":
                    definition.MaxProviderConcurrency ??= rule.Limit;
                    break;

                case "model":
                    definition.MaxModelConcurrency ??= rule.Limit;
                    break;

                case "operation":
                    definition.MaxOperationConcurrency ??= rule.Limit;
                    break;

                case "step":
                    definition.MaxStepConcurrency ??= rule.Limit;
                    break;

                case "step-type":
                    definition.MaxStepConcurrency ??= rule.Limit;
                    break;

                case "pipeline":
                    definition.MaxPipelineConcurrency ??= rule.Limit;
                    break;
            }

            if (rule.LeaseSeconds is > 0)
            {
                definition.LeaseSeconds = rule.LeaseSeconds.Value;
            }

            if (rule.DefaultRetryAfterMs is > 0)
            {
                definition.DefaultRetryAfterMs = rule.DefaultRetryAfterMs.Value;
            }
        }

        /// <summary>
        /// Determines whether a throttle rule is structurally valid.
        /// </summary>
        /// <param name="rule">
        /// The throttle rule.
        /// </param>
        /// <returns>
        /// <c>true</c> when the rule has a supported scope and positive limit.
        /// </returns>
        private static bool IsValid(
            AiConcurrencyThrottleRule rule)
        {
            if (rule.Limit <= 0)
            {
                return false;
            }

            return Normalize(rule.Scope) is
                "provider" or
                "model" or
                "operation" or
                "step" or
                "step-type" or
                "pipeline";
        }

        /// <summary>
        /// Determines whether a throttle rule matches the current concurrency context.
        /// </summary>
        /// <param name="rule">
        /// The throttle rule.
        /// </param>
        /// <param name="context">
        /// The concurrency context.
        /// </param>
        /// <returns>
        /// <c>true</c> when the rule applies to the context.
        /// </returns>
        private static bool Matches(
            AiConcurrencyThrottleRule rule,
            AiConcurrencyContext context)
        {
            var target = Normalize(rule.Target);

            if (string.IsNullOrWhiteSpace(target))
            {
                return true;
            }

            var scope = Normalize(rule.Scope);

            return scope switch
            {
                "provider" => target == Normalize(context.Provider),

                "model" => target == CreateProviderModelKey(
                    context.Provider,
                    context.Model),

                "operation" => target == Normalize(context.Operation),

                "step" => target == Normalize(context.StepId),

                "step-type" => target == Normalize(context.StepKey),

                "pipeline" => target == Normalize(context.PipelineKey),

                _ => false
            };
        }

        /// <summary>
        /// Creates the normalized provider/model key.
        /// </summary>
        /// <param name="provider">
        /// The provider value.
        /// </param>
        /// <param name="model">
        /// The model value.
        /// </param>
        /// <returns>
        /// The normalized provider/model key, or an empty string when either value is missing.
        /// </returns>
        private static string CreateProviderModelKey(
            string? provider,
            string? model)
        {
            var normalizedProvider = Normalize(provider);
            var normalizedModel = Normalize(model);

            return string.IsNullOrWhiteSpace(normalizedProvider) ||
                   string.IsNullOrWhiteSpace(normalizedModel)
                ? string.Empty
                : $"{normalizedProvider}:{normalizedModel}";
        }

        /// <summary>
        /// Clones a concurrency definition before applying targeted rules.
        /// </summary>
        /// <param name="definition">
        /// The source definition.
        /// </param>
        /// <returns>
        /// A copied definition.
        /// </returns>
        private static AiConcurrencyDefinition Clone(
            AiConcurrencyDefinition definition)
        {
            return new AiConcurrencyDefinition
            {
                Enabled = definition.Enabled,
                Policies = definition.Policies,
                ThrottleRules = definition.ThrottleRules,

                MaxDegreeOfParallelism = definition.MaxDegreeOfParallelism,

                MaxGlobalConcurrency = definition.MaxGlobalConcurrency,
                MaxPipelineConcurrency = definition.MaxPipelineConcurrency,
                MaxStepConcurrency = definition.MaxStepConcurrency,
                MaxExecutionConcurrency = definition.MaxExecutionConcurrency,
                MaxInstanceConcurrency = definition.MaxInstanceConcurrency,

                MaxProviderConcurrency = definition.MaxProviderConcurrency,
                MaxModelConcurrency = definition.MaxModelConcurrency,
                MaxOperationConcurrency = definition.MaxOperationConcurrency,

                LeaseSeconds = definition.LeaseSeconds,
                DefaultRetryAfterMs = definition.DefaultRetryAfterMs,

                Jitter = definition.Jitter,
                MaxJitterMs = definition.MaxJitterMs
            };
        }

        /// <summary>
        /// Normalizes values for case-insensitive comparison.
        /// </summary>
        /// <param name="value">
        /// The raw value.
        /// </param>
        /// <returns>
        /// The normalized value.
        /// </returns>
        private static string Normalize(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}