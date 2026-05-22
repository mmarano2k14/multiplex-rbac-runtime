using Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Options;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Interactive
{
    /// <summary>
    /// Applies interactive console selections to enterprise runtime demo options.
    /// </summary>
    public static class EnterpriseRuntimeInteractiveConsoleSelector
    {
        private const string NoneLogMode = "none";
        private const string VerboseLogMode = "verbose";
        private const string VerboseRawLogMode = "verbose + raw";
        private const string VerboseNoiseLogMode = "verbose + noise";

        /// <summary>
        /// Applies interactive scenario and log mode selections.
        /// </summary>
        /// <param name="options">
        /// The demo options to update.
        /// </param>
        /// <param name="scenarioNames">
        /// The available scenario names.
        /// </param>
        public static void Apply(
            EnterpriseRuntimeDemoOptions options,
            IReadOnlyList<string> scenarioNames)
        {
            ArgumentNullException.ThrowIfNull(
                options);

            ArgumentNullException.ThrowIfNull(
                scenarioNames);

            var selectedScenario = EnterpriseRuntimeConsoleMenu.Select(
                "Select scenario:",
                scenarioNames);

            var selectedLogMode = EnterpriseRuntimeConsoleMenu.Select(
                "Select log mode:",
                new[]
                {
                    NoneLogMode,
                    VerboseLogMode,
                    VerboseRawLogMode,
                    VerboseNoiseLogMode
                });

            options.Scenario = selectedScenario;

            ApplyLogMode(
                options,
                selectedLogMode);
        }

        /// <summary>
        /// Applies the selected log mode to the demo options.
        /// </summary>
        /// <param name="options">
        /// The demo options.
        /// </param>
        /// <param name="selectedLogMode">
        /// The selected log mode.
        /// </param>
        private static void ApplyLogMode(
            EnterpriseRuntimeDemoOptions options,
            string selectedLogMode)
        {
            options.Verbose = false;
            options.VerboseRaw = false;
            options.VerboseNoise = false;

            if (string.Equals(
                    selectedLogMode,
                    NoneLogMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(
                    selectedLogMode,
                    VerboseLogMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;

                return;
            }

            if (string.Equals(
                    selectedLogMode,
                    VerboseRawLogMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;
                options.VerboseRaw = true;

                return;
            }

            if (string.Equals(
                    selectedLogMode,
                    VerboseNoiseLogMode,
                    StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;
                options.VerboseNoise = true;
            }
        }
    }
}