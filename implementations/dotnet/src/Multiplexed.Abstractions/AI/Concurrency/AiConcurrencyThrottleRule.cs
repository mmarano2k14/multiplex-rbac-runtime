namespace Multiplexed.Abstractions.AI.Concurrency
{
    /// <summary>
    /// Defines a generic distributed concurrency throttling rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This rule is produced from configured concurrency policies such as:
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
    /// Supported scopes:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><description><c>provider</c> targets <see cref="AiConcurrencyContext.Provider"/>.</description></item>
    /// <item><description><c>model</c> targets <c>{Provider}:{Model}</c>.</description></item>
    /// <item><description><c>operation</c> targets <see cref="AiConcurrencyContext.Operation"/>.</description></item>
    /// <item><description><c>step</c> targets the concrete step name / step id.</description></item>
    /// <item><description><c>step-type</c> targets the logical step key.</description></item>
    /// <item><description><c>pipeline</c> targets the stable pipeline key.</description></item>
    /// </list>
    ///
    /// <para>
    /// When <see cref="Target"/> is <c>null</c> or empty, the rule applies to all values
    /// for the configured scope.
    /// </para>
    /// </remarks>
    public sealed class AiConcurrencyThrottleRule
    {
        /// <summary>
        /// Gets or sets the throttling scope.
        /// </summary>
        /// <remarks>
        /// Supported values are:
        /// <c>provider</c>, <c>model</c>, <c>operation</c>, <c>step</c>,
        /// <c>step-type</c>, and <c>pipeline</c>.
        /// </remarks>
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional target value for the scope.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Examples:
        /// </para>
        ///
        /// <list type="bullet">
        /// <item><description><c>provider</c> target: <c>openai</c></description></item>
        /// <item><description><c>model</c> target: <c>openai:gpt-4.1</c></description></item>
        /// <item><description><c>operation</c> target: <c>llm.chat</c></description></item>
        /// <item><description><c>step</c> target: concrete step name</description></item>
        /// <item><description><c>step-type</c> target: logical step key</description></item>
        /// <item><description><c>pipeline</c> target: stable pipeline key</description></item>
        /// </list>
        ///
        /// <para>
        /// If omitted, the rule applies to all targets for the configured scope.
        /// </para>
        /// </remarks>
        public string? Target { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent leases allowed for the matched scope.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the optional lease duration in seconds.
        /// </summary>
        /// <remarks>
        /// When configured, this value can override the effective lease duration for the matched rule.
        /// </remarks>
        public int? LeaseSeconds { get; set; }

        /// <summary>
        /// Gets or sets the optional retry-after delay in milliseconds when admission is denied.
        /// </summary>
        /// <remarks>
        /// When configured, this value can override the effective retry-after delay for the matched rule.
        /// </remarks>
        public int? DefaultRetryAfterMs { get; set; }
    }
}