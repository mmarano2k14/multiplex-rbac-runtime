using Multiplexed.Abstractions.AI.Policies;

namespace Multiplexed.AI.Runtime.AI.Policies
{
    /// <summary>
    /// Helper methods for configured AI policies.
    /// </summary>
    public static class AiConfiguredPolicyDefinitionExtensions
    {
        /// <summary>
        /// Returns ordered policy names.
        /// </summary>
        public static IReadOnlyList<string> GetPolicyNames(
            this IEnumerable<AiConfiguredPolicyDefinition>? policies)
        {
            if (policies is null)
            {
                return Array.Empty<string>();
            }

            return policies
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name)
                .ToList();
        }
    }
}