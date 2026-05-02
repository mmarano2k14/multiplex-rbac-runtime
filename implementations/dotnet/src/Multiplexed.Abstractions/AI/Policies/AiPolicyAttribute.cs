using System;

namespace Multiplexed.AI.Abstractions.AI.Policies
{
    /// <summary>
    /// Identifies a class as an AI policy and assigns it a stable runtime key.
    /// </summary>
    /// <remarks>
    /// This attribute is used by policy discovery and registration mechanisms to
    /// associate a concrete implementation with a stable policy key referenced
    /// from pipeline definitions and runtime configuration.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AiPolicyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiPolicyAttribute"/> class.
        /// </summary>
        /// <param name="key">
        /// The unique policy key used to resolve the policy at runtime.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="key"/> is null, empty, or whitespace.
        /// </exception>
        public AiPolicyAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Policy key cannot be null, empty, or whitespace.", nameof(key));
            }

            Key = key;
        }

        /// <summary>
        /// Gets the unique key used to identify and resolve the policy.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets or sets the category of the policy.
        /// </summary>
        /// <remarks>
        /// This value allows the runtime to filter and resolve policies
        /// based on their functional responsibility (e.g. retry, timeout, routing).
        /// </remarks>
        public AiPolicyKind Kind { get; init; }
    }
}