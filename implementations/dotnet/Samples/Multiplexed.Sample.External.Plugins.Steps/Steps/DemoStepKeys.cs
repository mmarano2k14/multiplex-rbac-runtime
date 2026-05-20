namespace Multiplexed.Sample.External.Plugins.Steps.Steps
{
    /// <summary>
    /// Defines the external demo step keys used by the enterprise runtime demo pipelines.
    /// </summary>
    public static class DemoStepKeys
    {
        /// <summary>
        /// Step key for a deterministic pass-through demo step.
        /// </summary>
        public const string Pass = "demo.pass";

        /// <summary>
        /// Step key for a deterministic delay demo step.
        /// </summary>
        public const string Delay = "demo.delay";

        /// <summary>
        /// Step key for a flaky demo step used to demonstrate retry behavior.
        /// </summary>
        public const string Flaky = "demo.flaky";

        /// <summary>
        /// Step key for a large payload demo step used to demonstrate retention and compaction.
        /// </summary>
        public const string LargePayload = "demo.large-payload";

        /// <summary>
        /// Step key for a throttled demo step used to demonstrate distributed concurrency limits.
        /// </summary>
        public const string Throttled = "demo.throttled";
    }
}