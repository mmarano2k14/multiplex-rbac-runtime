using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using System.Text.Json;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents a step-scoped execution context bound to a single resolved pipeline step.
    ///
    /// PURPOSE:
    /// - Exposes the global execution context to one concrete resolved step
    /// - Provides step-scoped access to state, inputs, configuration, and services
    /// - Supports both sequential and DAG-based execution without relying on mutable global step pointers
    ///
    /// DESIGN:
    /// - This context is created only after orchestration has selected a step to execute
    /// - It binds one <see cref="ResolvedAiPipelineStep"/> to one <see cref="AiExecutionContext"/>
    /// - The current step state is always resolved through <see cref="StepName"/>
    ///
    /// IMPORTANT:
    /// - This class does not own orchestration decisions
    /// - It is a read/write execution helper for the selected step only
    /// - Cross-step and global path resolution are delegated to the underlying execution context/state
    /// </summary>
    public sealed class AiStepExecutionContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepExecutionContext"/> class.
        ///
        /// During construction, the selected step is guaranteed to have
        /// an initialized durable <see cref="AiStepState"/> entry.
        /// </summary>
        /// <param name="execution">The global execution context for the current pipeline execution.</param>
        /// <param name="step">The resolved pipeline step bound to this context.</param>
        public AiStepExecutionContext(
            AiExecutionContext execution,
            ResolvedAiPipelineStep step)
        {
            ArgumentNullException.ThrowIfNull(execution);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentException.ThrowIfNullOrWhiteSpace(step.Name);

            Execution = execution;
            Step = step;

            // Ensure durable step state exists for this resolved step.
            Execution.State.EnsureStepInitialized(step);
        }

        /// <summary>
        /// Gets the global execution context.
        ///
        /// This gives access to shared execution state, services, bindings,
        /// and cross-step path resolution.
        /// </summary>
        public AiExecutionContext Execution { get; }

        /// <summary>
        /// Gets the resolved pipeline step bound to this context.
        ///
        /// This is the orchestration-selected step instance being executed.
        /// </summary>
        public ResolvedAiPipelineStep Step { get; }

        /// <summary>
        /// Gets the persisted execution record.
        ///
        /// This is the global orchestration summary, not the per-step source of truth.
        /// </summary>
        public AiExecutionRecord Record => Execution.Record;

        /// <summary>
        /// Gets the mutable execution state.
        ///
        /// This contains durable step state, shared state values, inputs, and config.
        /// </summary>
        public AiExecutionState State => Execution.State;

        /// <summary>
        /// Gets the scoped service provider available to the current execution.
        /// </summary>
        public IServiceProvider Services => Execution.Services;

        /// <summary>
        /// Gets the active cancellation token for the current execution scope.
        /// </summary>
        public CancellationToken CancellationToken => Execution.CancellationToken;

        /// <summary>
        /// Gets the execution identifier.
        /// </summary>
        public string ExecutionId => Execution.ExecutionId;

        /// <summary>
        /// Gets the logical step instance name inside the pipeline.
        ///
        /// This is the durable identity used to resolve <see cref="StepState"/>.
        /// </summary>
        public string StepName => Step.Name;

        /// <summary>
        /// Gets the step registry key / implementation key.
        ///
        /// This identifies the step type or implementation resolved by the registry.
        /// </summary>
        public string StepKey => Step.StepKey;

        /// <summary>
        /// Gets the durable mutable state for the current step instance.
        ///
        /// If the step state does not exist yet, it is created automatically.
        /// </summary>
        public AiStepState StepState => State.GetOrCreateStep(StepName);

        // ---------------------------------------------------------------------
        // STEP INPUT ACCESS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Retrieves a raw input value from the current step state using a direct key lookup.
        ///
        /// This method does not perform nested path traversal.
        /// </summary>
        public T? GetCurrentStepInputValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.GetStepInput<T>(StepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a raw input value from the current step state using a direct key lookup.
        ///
        /// This method does not perform nested path traversal.
        /// </summary>
        public bool TryGetCurrentStepInputValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.TryGetStepInput(StepName, key, out value);
        }

        /// <summary>
        /// Resolves a nested input value from the current step state using a dot-separated path.
        ///
        /// Example:
        /// <c>customer.address.city</c>
        /// </summary>
        public T? ResolveCurrentStepInput<T>(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return TryResolveNestedValue(StepState.Inputs, path, out T? value)
                ? value
                : default;
        }

        /// <summary>
        /// Attempts to resolve a nested input value from the current step state using a dot-separated path.
        /// </summary>
        public bool TryResolveCurrentStepInput<T>(string path, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return TryResolveNestedValue(StepState.Inputs, path, out value);
        }

        // ---------------------------------------------------------------------
        // STEP CONFIG ACCESS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Retrieves a raw configuration value from the current step state using a direct key lookup.
        ///
        /// This method does not perform nested path traversal.
        /// </summary>
        public T? GetCurrentStepConfigValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.GetStepConfig<T>(StepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a raw configuration value from the current step state using a direct key lookup.
        ///
        /// This method does not perform nested path traversal.
        /// </summary>
        public bool TryGetCurrentStepConfigValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.TryGetStepConfig(StepName, key, out value);
        }

        /// <summary>
        /// Resolves a nested configuration value from the current step state using a dot-separated path.
        ///
        /// Example:
        /// <c>retry.policy.delayMs</c>
        /// </summary>
        public T? ResolveCurrentStepConfig<T>(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return TryResolveNestedValue(StepState.Config, path, out T? value)
                ? value
                : default;
        }

        /// <summary>
        /// Attempts to resolve a nested configuration value from the current step state using a dot-separated path.
        /// </summary>
        public bool TryResolveCurrentStepConfig<T>(string path, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return TryResolveNestedValue(StepState.Config, path, out value);
        }

        // ---------------------------------------------------------------------
        // COMPATIBILITY HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Compatibility alias for <see cref="GetCurrentStepInputValue{T}(string)"/>.
        /// </summary>
        public T? GetStepInputValue<T>(string key) => GetCurrentStepInputValue<T>(key);

        /// <summary>
        /// Compatibility alias for <see cref="TryGetCurrentStepInputValue{T}(string, out T?)"/>.
        /// </summary>
        public bool TryGetStepInputValue<T>(string key, out T? value) =>
            TryGetCurrentStepInputValue(key, out value);

        /// <summary>
        /// Compatibility alias for <see cref="GetCurrentStepConfigValue{T}(string)"/>.
        /// </summary>
        public T? GetStepConfigValue<T>(string key) => GetCurrentStepConfigValue<T>(key);

        /// <summary>
        /// Compatibility alias for <see cref="TryGetCurrentStepConfigValue{T}(string, out T?)"/>.
        /// </summary>
        public bool TryGetStepConfigValue<T>(string key, out T? value) =>
            TryGetCurrentStepConfigValue(key, out value);

        // ---------------------------------------------------------------------
        // STEP BINDING RESOLUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolves a named input binding from the current step state.
        ///
        /// The stored step input is treated as a structured path and resolved against execution state.
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

            if (!State.TryGetStepInput(StepName, inputName, out string? path))
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

            if (!State.TryGetStepInput(StepName, inputName, out string? path) ||
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
        /// The stored configuration entry is treated as a structured path and resolved against execution state.
        /// </summary>
        public T? ResolveConfigBinding<T>(string configName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configName);

            if (!State.TryGetStepConfig(StepName, configName, out string? path))
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

            if (!State.TryGetStepConfig(StepName, configName, out string? path) ||
                string.IsNullOrWhiteSpace(path))
            {
                value = default;
                return false;
            }

            return TryResolvePath(path, out value);
        }

        // ---------------------------------------------------------------------
        // CROSS-STEP / GLOBAL PATH RESOLUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolves a structured path against the execution state.
        ///
        /// This is the main entry point for cross-step and global binding resolution.
        /// </summary>
        public T? ResolvePath<T>(string path)
        {
            return Execution.ResolvePath<T>(path);
        }

        /// <summary>
        /// Attempts to resolve a structured path against the execution state.
        /// </summary>
        public bool TryResolvePath<T>(string path, out T? value)
        {
            return Execution.TryResolvePath(path, out value);
        }

        /// <summary>
        /// Retrieves a configuration value from a specific step by direct key lookup.
        /// </summary>
        public T? GetStepConfig<T>(string stepName, string key)
        {
            return State.GetStepConfig<T>(stepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a configuration value from a specific step by direct key lookup.
        /// </summary>
        public bool TryGetStepConfig<T>(string stepName, string key, out T? value)
        {
            return State.TryGetStepConfig(stepName, key, out value);
        }

        /// <summary>
        /// Resolves a required scoped service from the execution context.
        /// </summary>
        public T GetRequiredService<T>() where T : notnull
        {
            return Execution.GetRequiredService<T>();
        }

        // ---------------------------------------------------------------------
        // INTERNAL HELPERS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Attempts to resolve a nested value from a source object using a dot-separated path.
        ///
        /// Supported source types:
        /// - <see cref="IDictionary{TKey,TValue}"/>
        /// - <see cref="IReadOnlyDictionary{TKey,TValue}"/>
        /// - <see cref="JsonElement"/> objects
        ///
        /// Conversion behavior:
        /// - If the final value already matches <typeparamref name="T"/>, it is returned directly
        /// - If the final value is a <see cref="JsonElement"/>, JSON deserialization is attempted
        /// - Otherwise <see cref="Convert.ChangeType(object, Type)"/> is attempted
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
    }
}