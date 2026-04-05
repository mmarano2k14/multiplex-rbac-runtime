using Multiplexed.Abstractions.AI.Pipeline;
using System.Linq;
using System.Text.Json;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the shared execution context passed across AI runtime components.
    ///
    /// PURPOSE:
    /// - Exposes the global execution record and mutable execution state
    /// - Provides access to scoped services and cancellation
    /// - Offers helper APIs for step-scoped reads and structured path resolution
    ///
    /// DESIGN:
    /// - This is the shared execution-level context
    /// - It may be used directly by runtime components or wrapped by <see cref="AiStepExecutionContext"/>
    /// - It supports both legacy metadata-based "current step" access and newer path-based step resolution
    ///
    /// IMPORTANT:
    /// - <see cref="AiExecutionRecord"/> is the orchestration summary
    /// - <see cref="AiExecutionState"/> contains the mutable runtime data and durable per-step state
    /// - This class does not decide orchestration order; it only exposes execution data and helpers
    /// </summary>
    public sealed class AiExecutionContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiExecutionContext"/> class.
        /// </summary>
        /// <param name="record">The persisted execution record.</param>
        /// <param name="state">The mutable execution state.</param>
        /// <param name="services">The scoped service provider.</param>
        /// <param name="cancellationToken">The active cancellation token.</param>
        public AiExecutionContext(
            AiExecutionRecord record,
            AiExecutionState state,
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(services);

            Record = record;
            State = state;
            Services = services;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the persisted orchestration record.
        ///
        /// This is the global execution summary used for orchestration visibility.
        /// It is not the per-step source of truth for DAG scheduling.
        /// </summary>
        public AiExecutionRecord Record { get; }

        /// <summary>
        /// Gets the mutable execution state.
        ///
        /// This contains shared state values, metadata, and step-scoped runtime data.
        /// </summary>
        public AiExecutionState State { get; }

        /// <summary>
        /// Gets the scoped service provider available to the current execution.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the current execution identifier.
        /// </summary>
        public string ExecutionId => Record.ExecutionId;

        /// <summary>
        /// Gets the active cancellation token for the current execution scope.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        // ---------------------------------------------------------------------
        // LEGACY SHARED STATE ACCESS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Stores or replaces a value in the legacy shared execution state bag.
        ///
        /// This is retained for compatibility with existing runtime flows that still use
        /// the global state bag in addition to step-scoped state.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            State.Set(key, value);
        }

        /// <summary>
        /// Retrieves a strongly-typed value from the legacy shared execution state bag.
        /// </summary>
        public T? Get<T>(string key)
        {
            return State.Get<T>(key);
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the legacy shared execution state bag.
        /// </summary>
        public bool TryGet<T>(string key, out T? value)
        {
            return State.TryGet(key, out value);
        }

        // ---------------------------------------------------------------------
        // STEP-SCOPED STATE ACCESS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Retrieves a raw input value from the current step state using a direct key lookup.
        ///
        /// This method depends on the current step name being present in execution metadata.
        /// It does not perform nested path traversal.
        /// </summary>
        public T? GetCurrentStepInputValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var stepName = GetRequiredCurrentStepName();
            return State.GetStepInput<T>(stepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a raw input value from the current step state using a direct key lookup.
        ///
        /// This method depends on the current step name being present in execution metadata.
        /// It does not perform nested path traversal.
        /// </summary>
        public bool TryGetCurrentStepInputValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var stepName = GetCurrentStepName();
            if (string.IsNullOrWhiteSpace(stepName))
            {
                value = default;
                return false;
            }

            return State.TryGetStepInput(stepName, key, out value);
        }

        /// <summary>
        /// Resolves a nested input value from the current step state using a dot-separated path.
        ///
        /// Examples:
        /// - <c>auth.userName</c>
        /// - <c>payload.customer.id</c>
        ///
        /// This method depends on the current step name being present in execution metadata.
        /// </summary>
        public T? ResolveCurrentStepInput<T>(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var stepName = GetRequiredCurrentStepName();

            if (!State.Steps.TryGetValue(stepName, out var step))
                return default;

            return TryResolveNestedValue(step.Inputs, path, out T? value)
                ? value
                : default;
        }

        /// <summary>
        /// Attempts to resolve a nested input value from the current step state using a dot-separated path.
        /// </summary>
        public bool TryResolveCurrentStepInput<T>(string path, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var stepName = GetCurrentStepName();
            if (string.IsNullOrWhiteSpace(stepName) ||
                !State.Steps.TryGetValue(stepName, out var step))
            {
                value = default;
                return false;
            }

            return TryResolveNestedValue(step.Inputs, path, out value);
        }

        /// <summary>
        /// Retrieves a raw configuration value from the current step state using a direct key lookup.
        ///
        /// This method depends on the current step name being present in execution metadata.
        /// It does not perform nested path traversal.
        /// </summary>
        public T? GetCurrentStepConfigValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var stepName = GetRequiredCurrentStepName();
            return State.GetStepConfig<T>(stepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a raw configuration value from the current step state using a direct key lookup.
        ///
        /// This method depends on the current step name being present in execution metadata.
        /// It does not perform nested path traversal.
        /// </summary>
        public bool TryGetCurrentStepConfigValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var stepName = GetCurrentStepName();
            if (string.IsNullOrWhiteSpace(stepName))
            {
                value = default;
                return false;
            }

            return State.TryGetStepConfig(stepName, key, out value);
        }

        /// <summary>
        /// Resolves a nested configuration value from the current step state using a dot-separated path.
        ///
        /// Examples:
        /// - <c>auth.password</c>
        /// - <c>data.connection.timeout</c>
        ///
        /// This method depends on the current step name being present in execution metadata.
        /// </summary>
        public T? ResolveCurrentStepConfig<T>(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var stepName = GetRequiredCurrentStepName();

            if (!State.Steps.TryGetValue(stepName, out var step))
                return default;

            return TryResolveNestedValue(step.Config, path, out T? value)
                ? value
                : default;
        }

        /// <summary>
        /// Attempts to resolve a nested configuration value from the current step state using a dot-separated path.
        /// </summary>
        public bool TryResolveCurrentStepConfig<T>(string path, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var stepName = GetCurrentStepName();
            if (string.IsNullOrWhiteSpace(stepName) ||
                !State.Steps.TryGetValue(stepName, out var step))
            {
                value = default;
                return false;
            }

            return TryResolveNestedValue(step.Config, path, out value);
        }

        /// <summary>
        /// Retrieves a configuration value from a specific step using a direct key lookup.
        ///
        /// Returns the default value when the step or key does not exist.
        /// </summary>
        public T? GetStepConfig<T>(string stepName, string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return State.GetStepConfig<T>(stepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a configuration value from a specific step using a direct key lookup.
        /// </summary>
        public bool TryGetStepConfig<T>(string stepName, string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return State.TryGetStepConfig(stepName, key, out value);
        }

        /// <summary>
        /// Resolves a required scoped service from the execution context.
        ///
        /// Throws when the requested service is not registered.
        /// </summary>
        public T GetRequiredService<T>() where T : notnull
        {
            return (T)(Services.GetService(typeof(T))
                ?? throw new InvalidOperationException(
                    $"Required service '{typeof(T).FullName}' is not registered."));
        }

        // ---------------------------------------------------------------------
        // STEP METADATA HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets the current step name from execution metadata, if available.
        ///
        /// This supports compatibility with runtime components that still track the
        /// active step through execution metadata.
        /// </summary>
        public string? GetCurrentStepName()
        {
            return TryGetMetadata<string>(AiExecutionKeys.CurrentStepName, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Gets the current step key from execution metadata, if available.
        ///
        /// This typically corresponds to the resolved registry key / implementation key.
        /// </summary>
        public string? GetCurrentStepKey()
        {
            return TryGetMetadata<string>(AiExecutionKeys.CurrentStepKey, out var value)
                ? value
                : null;
        }

        // ---------------------------------------------------------------------
        // COMPATIBILITY HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Compatibility alias for <see cref="GetCurrentStepInputValue{T}(string)"/>.
        ///
        /// This now reads from the current step state rather than from legacy global metadata.
        /// </summary>
        public T? GetStepInputValue<T>(string key)
        {
            return GetCurrentStepInputValue<T>(key);
        }

        /// <summary>
        /// Compatibility alias for <see cref="TryGetCurrentStepInputValue{T}(string, out T?)"/>.
        ///
        /// This now reads from the current step state rather than from legacy global metadata.
        /// </summary>
        public bool TryGetStepInputValue<T>(string key, out T? value)
        {
            return TryGetCurrentStepInputValue(key, out value);
        }

        /// <summary>
        /// Compatibility alias for <see cref="GetCurrentStepConfigValue{T}(string)"/>.
        ///
        /// This now reads from the current step state rather than from legacy global metadata.
        /// </summary>
        public T? GetStepConfigValue<T>(string key)
        {
            return GetCurrentStepConfigValue<T>(key);
        }

        /// <summary>
        /// Compatibility alias for <see cref="TryGetCurrentStepConfigValue{T}(string, out T?)"/>.
        ///
        /// This now reads from the current step state rather than from legacy global metadata.
        /// </summary>
        public bool TryGetStepConfigValue<T>(string key, out T? value)
        {
            return TryGetCurrentStepConfigValue(key, out value);
        }

        // ---------------------------------------------------------------------
        // STEP BINDING RESOLUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolves a named input binding from the current step state.
        ///
        /// Behavior:
        /// 1. Reads the raw input value from the current step inputs
        /// 2. Treats that value as a structured path
        /// 3. Resolves the path against the execution state
        ///
        /// Supported path formats:
        /// - <c>steps.{stepName}.inputs.{path}</c>
        /// - <c>steps.{stepName}.config.{path}</c>
        /// - <c>steps.{stepName}.result.value</c>
        /// - <c>steps.{stepName}.result.data.{path}</c>
        /// - <c>state.{path}</c>
        /// </summary>
        public T? ResolveInputBinding<T>(string inputName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputName);

            var currentStepName = GetRequiredCurrentStepName();

            if (!State.TryGetStepInput(currentStepName, inputName, out string? path))
                return default;

            if (string.IsNullOrWhiteSpace(path))
                return default;

            return ResolvePath<T>(path);
        }

        /// <summary>
        /// Attempts to resolve a named input binding from the current step state.
        /// </summary>
        public bool TryResolveInputBinding<T>(string inputName, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputName);

            var currentStepName = GetRequiredCurrentStepName();

            if (!State.TryGetStepInput(currentStepName, inputName, out string? path) ||
                string.IsNullOrWhiteSpace(path))
            {
                value = default;
                return false;
            }

            return TryResolvePath(path, out value);
        }

        /// <summary>
        /// Resolves a named configuration binding from the current step state.
        ///
        /// Behavior:
        /// 1. Reads the raw configuration value from the current step config
        /// 2. Treats that value as a structured path
        /// 3. Resolves the path against the execution state
        /// </summary>
        public T? ResolveConfigBinding<T>(string configName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configName);

            var currentStepName = GetRequiredCurrentStepName();

            if (!State.TryGetStepConfig(currentStepName, configName, out string? path))
                return default;

            if (string.IsNullOrWhiteSpace(path))
                return default;

            return ResolvePath<T>(path);
        }

        /// <summary>
        /// Attempts to resolve a named configuration binding from the current step state.
        /// </summary>
        public bool TryResolveConfigBinding<T>(string configName, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configName);

            var currentStepName = GetRequiredCurrentStepName();

            if (!State.TryGetStepConfig(currentStepName, configName, out string? path) ||
                string.IsNullOrWhiteSpace(path))
            {
                value = default;
                return false;
            }

            return TryResolvePath(path, out value);
        }

        /// <summary>
        /// Resolves a structured path against the execution state.
        ///
        /// Supported path formats:
        /// - <c>steps.{stepName}.inputs.{path}</c>
        /// - <c>steps.{stepName}.config.{path}</c>
        /// - <c>steps.{stepName}.result.value</c>
        /// - <c>steps.{stepName}.result.data.{path}</c>
        /// - <c>state.{path}</c>
        /// </summary>
        public T? ResolvePath<T>(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            if (TryResolvePath(path, out T? value))
                return value;

            return default;
        }

        /// <summary>
        /// Attempts to resolve a structured path against the execution state.
        ///
        /// Path resolution supports:
        /// - step input dictionaries
        /// - step config dictionaries
        /// - step result values and result data
        /// - legacy shared state under <c>state.*</c>
        /// </summary>
        public bool TryResolvePath<T>(string path, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 4 &&
                parts[0].Equals("steps", StringComparison.OrdinalIgnoreCase))
            {
                var stepName = parts[1];
                var scope = parts[2];

                if (!State.Steps.TryGetValue(stepName, out var step))
                {
                    value = default;
                    return false;
                }

                if (scope.Equals("inputs", StringComparison.OrdinalIgnoreCase))
                {
                    var nestedPath = string.Join('.', parts.Skip(3));
                    return TryResolveNestedValue(step.Inputs, nestedPath, out value);
                }

                if (scope.Equals("config", StringComparison.OrdinalIgnoreCase))
                {
                    var nestedPath = string.Join('.', parts.Skip(3));
                    return TryResolveNestedValue(step.Config, nestedPath, out value);
                }

                if (scope.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    if (step.Result is null)
                    {
                        value = default;
                        return false;
                    }

                    if (parts.Length == 4 &&
                        parts[3].Equals("value", StringComparison.OrdinalIgnoreCase))
                    {
                        return step.Result.TryGetValue(out value);
                    }

                    if (parts.Length >= 5 &&
                        parts[3].Equals("data", StringComparison.OrdinalIgnoreCase))
                    {
                        var nestedPath = string.Join('.', parts.Skip(4));
                        return TryResolveNestedValue(step.Result.Data, nestedPath, out value);
                    }
                }
            }

            if (parts.Length >= 2 &&
                parts[0].Equals("state", StringComparison.OrdinalIgnoreCase))
            {
                var nestedPath = string.Join('.', parts.Skip(1));

#pragma warning disable CS0618
                return TryResolveNestedValue(State.Data, nestedPath, out value);
#pragma warning restore CS0618
            }

            value = default;
            return false;
        }

        // ---------------------------------------------------------------------
        // METADATA
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to get a strongly-typed execution metadata value.
        ///
        /// Supports values restored from JSON as <see cref="JsonElement"/>,
        /// which commonly occurs when metadata is loaded from JSON-backed providers.
        /// </summary>
        private bool TryGetMetadata<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!State.Metadata.TryGetValue(key, out var rawValue))
            {
                value = default;
                return false;
            }

            if (rawValue is null)
            {
                value = default;
                return false;
            }

            if (rawValue is T typed)
            {
                value = typed;
                return true;
            }

            if (rawValue is JsonElement jsonElement)
            {
                try
                {
                    value = ConvertJsonElement<T>(key, jsonElement, "Execution metadata");
                    return true;
                }
                catch
                {
                    value = default;
                    return false;
                }
            }

            throw new InvalidCastException(
                $"Execution metadata key '{key}' contains type '{rawValue.GetType().Name}' but was requested as '{typeof(T).Name}'.");
        }

        // ---------------------------------------------------------------------
        // NESTED VALUE RESOLUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to resolve a nested value from a dictionary-like object graph
        /// using a dot-separated path.
        ///
        /// Supported sources:
        /// - <see cref="IDictionary{TKey,TValue}"/>
        /// - <see cref="IReadOnlyDictionary{TKey,TValue}"/>
        /// - <see cref="JsonElement"/> objects
        /// </summary>
        private static bool TryResolveNestedValue<T>(
            object? source,
            string path,
            out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            object? current = source;

            foreach (var part in parts)
            {
                if (current is null)
                {
                    value = default;
                    return false;
                }

                if (current is IDictionary<string, object?> dict)
                {
                    if (!dict.TryGetValue(part, out current))
                    {
                        value = default;
                        return false;
                    }

                    continue;
                }

                if (current is IReadOnlyDictionary<string, object?> readOnlyDict)
                {
                    if (!readOnlyDict.TryGetValue(part, out current))
                    {
                        value = default;
                        return false;
                    }

                    continue;
                }

                if (current is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Object &&
                        jsonElement.TryGetProperty(part, out var child))
                    {
                        current = child;
                        continue;
                    }

                    value = default;
                    return false;
                }

                value = default;
                return false;
            }

            if (current is null)
            {
                value = default;
                return true;
            }

            if (current is T typed)
            {
                value = typed;
                return true;
            }

            if (current is JsonElement finalJson)
            {
                try
                {
                    value = finalJson.Deserialize<T>();
                    return true;
                }
                catch
                {
                    value = default;
                    return false;
                }
            }

            try
            {
                value = (T?)Convert.ChangeType(current, typeof(T));
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        // ---------------------------------------------------------------------
        // INTERNAL HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets the current step name from execution metadata or throws when unavailable.
        ///
        /// This helper exists for compatibility with flows that still depend on
        /// metadata-based current-step resolution.
        /// </summary>
        private string GetRequiredCurrentStepName()
        {
            var stepName = GetCurrentStepName();

            if (string.IsNullOrWhiteSpace(stepName))
            {
                throw new InvalidOperationException(
                    "The current step name is not available in execution metadata.");
            }

            return stepName;
        }

        /// <summary>
        /// Converts a JSON-backed value into the requested target type.
        ///
        /// This is required because <see cref="System.Text.Json"/> commonly restores
        /// object-based metadata values as <see cref="JsonElement"/>.
        /// </summary>
        private static T? ConvertJsonElement<T>(string key, JsonElement jsonElement, string scope)
        {
            try
            {
                if (typeof(T) == typeof(string) && jsonElement.ValueKind == JsonValueKind.String)
                {
                    object? value = jsonElement.GetString();
                    return (T?)value;
                }

                return jsonElement.Deserialize<T>();
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"{scope} key '{key}' contains JSON value '{jsonElement.ValueKind}' but could not be converted to '{typeof(T).Name}'.",
                    ex);
            }
        }

        private static readonly IReadOnlyDictionary<string, object?> EmptyDictionary =
            new Dictionary<string, object?>();
    }
}