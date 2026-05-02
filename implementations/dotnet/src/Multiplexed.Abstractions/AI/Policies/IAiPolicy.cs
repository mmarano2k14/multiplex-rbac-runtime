namespace Multiplexed.AI.Abstractions.AI.Policies
{
    /// <summary>
    /// Defines the base contract for all AI policies.
    /// </summary>
    /// <remarks>
    /// AI policies provide a unified abstraction for extending runtime behavior
    /// such as retry logic, timeouts, routing, validation, and rate limiting.
    /// 
    /// Specialized policy types should extend this interface and add behavior
    /// specific to their domain.
    /// </remarks>
    public interface IAiPolicy
    {
        /// <summary>
        /// Gets the unique key used to identify and resolve the policy.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the category of the policy.
        /// </summary>
        AiPolicyKind Kind { get; }
    }
}