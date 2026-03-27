using Multiplexed.Abstractions.AI.Pipeline;
using System.Text.Json;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents the shared execution context passed across AI runtime components.
    /// Provides access to orchestration record, mutable state, scoped services,
    /// and helper APIs for declarative step metadata.
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
        /// </summary>
        public AiExecutionRecord Record { get; }

        /// <summary>
        /// Gets the mutable execution state.
        /// </summary>
        public AiExecutionState State { get; }

        /// <summary>
        /// Gets the scoped service provider.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the current execution identifier.
        /// </summary>
        public string ExecutionId => Record.ExecutionId;

        /// <summary>
        /// Gets the active cancellation token.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Stores or replaces a value in the execution state data.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            State.Set(key, value);
        }

        /// <summary>
        /// Retrieves a strongly-typed value from the execution state data.
        /// </summary>
        public T? Get<T>(string key)
        {
            return State.Get<T>(key);
        }

        /// <summary>
        /// Attempts to retrieve a strongly-typed value from the execution state data.
        /// </summary>
        public bool TryGet<T>(string key, out T? value)
        {
            return State.TryGet(key, out value);
        }

        /// <summary>
        /// Resolves a required scoped service from the execution context.
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
        /// </summary>
        public string? GetCurrentStepName()
        {
            return TryGetMetadata<string>(AiExecutionKeys.CurrentStepName, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Gets the current step key from execution metadata, if available.
        /// </summary>
        public string? GetCurrentStepKey()
        {
            return TryGetMetadata<string>(AiExecutionKeys.CurrentStepKey, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Gets the full current step input mapping from execution metadata.
        /// Returns an empty dictionary if no step input is available.
        /// </summary>
        public IReadOnlyDictionary<string, object?> GetStepInput()
        {
            return TryGetMetadata<IReadOnlyDictionary<string, object?>>(AiExecutionKeys.CurrentStepInput, out var value)
                ? value ?? EmptyDictionary
                : EmptyDictionary;
        }

        /// <summary>
        /// Gets the full current step configuration from execution metadata.
        /// Returns an empty dictionary if no step configuration is available.
        /// </summary>
        public IReadOnlyDictionary<string, object?> GetStepConfig()
        {
            return TryGetMetadata<IReadOnlyDictionary<string, object?>>(AiExecutionKeys.CurrentStepConfig, out var value)
                ? value ?? EmptyDictionary
                : EmptyDictionary;
        }

        /// <summary>
        /// Gets a strongly-typed configuration value for the current step.
        /// Returns default if the key is not present.
        /// Throws if the stored value cannot be cast to the requested type.
        /// Also supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public T? GetStepConfigValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var config = GetStepConfig();

            if (!config.TryGetValue(key, out var value))
                return default;

            if (value is null)
                return default;

            if (value is T typed)
                return typed;

            if (value is JsonElement jsonElement)
                return ConvertJsonElement<T>(key, jsonElement, "Step config");

            throw new InvalidCastException(
                $"Step config key '{key}' contains type '{value.GetType().Name}' but was requested as '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Attempts to get a strongly-typed configuration value for the current step.
        /// Returns false if the key is not present or the value is null.
        /// Also supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public bool TryGetStepConfigValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var config = GetStepConfig();

            if (!config.TryGetValue(key, out var rawValue))
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
                    value = ConvertJsonElement<T>(key, jsonElement, "Step config");
                    return true;
                }
                catch
                {
                    value = default;
                    return false;
                }
            }

            throw new InvalidCastException(
                $"Step config key '{key}' contains type '{rawValue.GetType().Name}' but was requested as '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Gets a strongly-typed input mapping value for the current step.
        /// Returns default if the key is not present.
        /// Throws if the stored value cannot be cast to the requested type.
        /// Also supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public T? GetStepInputValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var input = GetStepInput();

            if (!input.TryGetValue(key, out var value))
                return default;

            if (value is null)
                return default;

            if (value is T typed)
                return typed;

            if (value is JsonElement jsonElement)
                return ConvertJsonElement<T>(key, jsonElement, "Step input");

            throw new InvalidCastException(
                $"Step input key '{key}' contains type '{value.GetType().Name}' but was requested as '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Attempts to get a strongly-typed input mapping value for the current step.
        /// Returns false if the key is not present or the value is null.
        /// Also supports values restored from JSON as <see cref="JsonElement"/>.
        /// </summary>
        public bool TryGetStepInputValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var input = GetStepInput();

            if (!input.TryGetValue(key, out var rawValue))
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
                    value = ConvertJsonElement<T>(key, jsonElement, "Step input");
                    return true;
                }
                catch
                {
                    value = default;
                    return false;
                }
            }

            throw new InvalidCastException(
                $"Step input key '{key}' contains type '{rawValue.GetType().Name}' but was requested as '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Resolves a named input binding from the current step input mapping
        /// against the execution state data.
        ///
        /// Example:
        /// - Step input mapping: { "query": "input" }
        /// - State data: { "input": "hello" }
        /// - ResolveInputBinding&lt;string&gt;("query") returns "hello"
        /// </summary>
        public T? ResolveInputBinding<T>(string inputName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputName);

            var bindingKey = GetStepInputValue<string>(inputName);

            if (string.IsNullOrWhiteSpace(bindingKey))
                return default;

            return Get<T>(bindingKey);
        }

        /// <summary>
        /// Attempts to resolve a named input binding from the current step input mapping
        /// against the execution state data.
        /// </summary>
        public bool TryResolveInputBinding<T>(string inputName, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputName);

            var bindingKey = GetStepInputValue<string>(inputName);

            if (string.IsNullOrWhiteSpace(bindingKey))
            {
                value = default;
                return false;
            }

            return TryGet(bindingKey, out value);
        }

        /// <summary>
        /// Attempts to get a strongly-typed execution metadata value.
        /// Also supports values restored from JSON as <see cref="JsonElement"/>.
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

        /// <summary>
        /// Converts a JSON-backed value into the requested target type.
        ///
        /// This is required when pipeline definitions or step metadata are loaded
        /// from JSON providers, because <see cref="System.Text.Json"/> deserializes
        /// object-based values as <see cref="JsonElement"/>.
        /// </summary>
        private static T? ConvertJsonElement<T>(string key, JsonElement jsonElement, string scope)
        {
            try
            {
                // Fast-path for plain string values.
                if (typeof(T) == typeof(string) && jsonElement.ValueKind == JsonValueKind.String)
                {
                    object? value = jsonElement.GetString();
                    return (T?)value;
                }

                // Generic conversion for booleans, numbers, objects, arrays, etc.
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