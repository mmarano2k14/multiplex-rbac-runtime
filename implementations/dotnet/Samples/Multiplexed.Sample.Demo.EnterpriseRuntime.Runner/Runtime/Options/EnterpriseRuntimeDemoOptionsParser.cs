namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Options
{
    /// <summary>
    /// Parses enterprise runtime demo command-line options.
    /// </summary>
    public static class EnterpriseRuntimeDemoOptionsParser
    {
        /// <summary>
        /// Parses command-line arguments.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments.
        /// </param>
        /// <returns>
        /// The parsed demo options.
        /// </returns>
        public static EnterpriseRuntimeDemoOptions Parse(
            IReadOnlyList<string> args)
        {
            var options = new EnterpriseRuntimeDemoOptions();

            for (var index = 0; index < args.Count; index++)
            {
                var argument = args[index];

                if (string.Equals(
                        argument,
                        "--start-infrastructure",
                        StringComparison.OrdinalIgnoreCase))
                {
                    options.StartInfrastructure = true;

                    continue;
                }

                if (string.Equals(
                        argument,
                        "--scenario",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Count)
                    {
                        throw new InvalidOperationException(
                            "Missing scenario name after '--scenario'.");
                    }

                    options.Scenario = args[index + 1];

                    index++;

                    continue;
                }

                if (string.Equals(
                        argument,
                        "--verbose",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        argument,
                        "-v",
                        StringComparison.OrdinalIgnoreCase))
                {
                    options.Verbose = true;

                    continue;
                }

                if (string.Equals(
                        argument,
                        "--verbose-raw",
                        StringComparison.OrdinalIgnoreCase))
                {
                    options.Verbose = true;
                    options.VerboseRaw = true;

                    continue;
                }

                if (string.Equals(
                        argument,
                        "--verbose-noise",
                        StringComparison.OrdinalIgnoreCase))
                {
                    options.Verbose = true;
                    options.VerboseNoise = true;

                    continue;
                }
            }

            return options;
        }
    }
}