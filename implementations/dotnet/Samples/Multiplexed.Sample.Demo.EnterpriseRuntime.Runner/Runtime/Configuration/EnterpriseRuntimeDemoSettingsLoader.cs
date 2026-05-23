using System.Text.Json;

namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Configuration
{
    /// <summary>
    /// Loads enterprise runtime demo settings from JSON configuration.
    /// </summary>
    public static class EnterpriseRuntimeDemoSettingsLoader
    {
        private const string DefaultSettingsPath =
            "config/enterprise-runtime-settings.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Loads enterprise runtime demo settings from the default configuration path.
        /// </summary>
        /// <returns>
        /// The loaded enterprise runtime demo settings.
        /// </returns>
        public static EnterpriseRuntimeDemoSettings Load()
        {
            return Load(
                DefaultSettingsPath);
        }

        /// <summary>
        /// Loads enterprise runtime demo settings from a configuration path.
        /// </summary>
        /// <param name="path">
        /// The settings file path.
        /// </param>
        /// <returns>
        /// The loaded enterprise runtime demo settings.
        /// </returns>
        public static EnterpriseRuntimeDemoSettings Load(
            string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                path);

            if (!File.Exists(
                    path))
            {
                return new EnterpriseRuntimeDemoSettings();
            }

            var json = File.ReadAllText(
                path);

            var settings = JsonSerializer.Deserialize<EnterpriseRuntimeDemoSettings>(
                json,
                JsonOptions);

            return settings ?? new EnterpriseRuntimeDemoSettings();
        }
    }
}