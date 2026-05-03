namespace Multiplexed.AI.Abstractions.AI.Policies
{
    /// <summary>
    /// Represents a strongly typed result of an AI policy execution.
    /// </summary>
    /// <typeparam name="T">The type of data produced by the policy result.</typeparam>
    /// <remarks>
    /// This class extends <see cref="AiPolicyResult"/> by providing strongly typed data
    /// that can be consumed safely by runtime engines such as retry, retention,
    /// eviction, or recovery.
    /// </remarks>
    public sealed class AiPolicyResultGeneric<T> : AiPolicyResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiPolicyResultGeneric{T}"/> class.
        /// </summary>
        /// <param name="kind">The policy result kind.</param>
        /// <param name="data">The strongly typed data produced by the policy.</param>
        /// <param name="message">An optional message describing the result.</param>
        internal AiPolicyResultGeneric(
            AiPolicyResultKind kind,
            T? data,
            string? message = null)
            : base(kind, message)
        {
            Data = data;
        }

        /// <summary>
        /// Gets the strongly typed data produced by the policy.
        /// </summary>
        public T? Data { get; }
    }
}