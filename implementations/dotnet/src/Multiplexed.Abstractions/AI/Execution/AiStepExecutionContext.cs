using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using System.Text.Json;

namespace Multiplexed.Abstractions.AI.Execution
{
    /// <summary>
    /// Represents a step-scoped execution context bound to a single resolved pipeline step.
    ///
    /// This context is created after pipeline orchestration has selected the step to execute.
    /// It provides:
    /// - access to the global execution context
    /// - access to the resolved step definition
    /// - step-scoped input/config resolution
    /// - cross-step path resolution through the underlying execution state
    ///
    /// This design avoids relying on global mutable metadata such as "CurrentStepName"
    /// and enables safe sequential or DAG-based execution models.
    /// </summary>
    public sealed class AiStepExecutionContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepExecutionContext"/> class.
        /// </summary>
        /// <param name="execution">The global execution context.</param>
        /// <param name="step">The resolved pipeline step bound to this execution scope.</param>
        public AiStepExecutionContext(
            AiExecutionContext execution,
            ResolvedAiPipelineStep step)
        {
            ArgumentNullException.ThrowIfNull(execution);
            ArgumentNullException.ThrowIfNull(step);
            ArgumentException.ThrowIfNullOrWhiteSpace(step.Name);

            Execution = execution;

            Step = step;

            Execution.State.EnsureStepInitialized(step);
        }

        /// <summary>
        /// Gets the global execution context.
        /// </summary>
        public AiExecutionContext Execution { get; }

        /// <summary>
        /// Gets the resolved pipeline step bound to this context.
        /// </summary>
        public ResolvedAiPipelineStep Step { get; }

        /// <summary>
        /// Gets the persisted orchestration record.
        /// </summary>
        public AiExecutionRecord Record => Execution.Record;

        /// <summary>
        /// Gets the mutable execution state.
        /// </summary>
        public AiExecutionState State => Execution.State;

        /// <summary>
        /// Gets the scoped service provider.
        /// </summary>
        public IServiceProvider Services => Execution.Services;

        /// <summary>
        /// Gets the active cancellation token.
        /// </summary>
        public CancellationToken CancellationToken => Execution.CancellationToken;

        /// <summary>
        /// Gets the execution identifier.
        /// </summary>
        public string ExecutionId => Execution.ExecutionId;

        /// <summary>
        /// Gets the unique step instance name inside the pipeline.
        /// </summary>
        public string StepName => Step.Name;

        /// <summary>
        /// Gets the step registry key / type key.
        /// </summary>
        public string StepKey => Step.StepKey;

        /// <summary>
        /// Gets the mutable state for the current step instance.
        /// Creates it when missing.
        /// </summary>
        public AiStepState StepState => State.GetOrCreateStep(StepName);

        // ---------------------------------------------------------------------
        // STEP INPUT ACCESS
        // ---------------------------------------------------------------------

        /// <summary>
        /// Retrieves a raw input value from the current step state.
        /// This method performs a direct key lookup only.
        /// </summary>
        public T? GetCurrentStepInputValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.GetStepInput<T>(StepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a raw input value from the current step state.
        /// This method performs a direct key lookup only.
        /// </summary>
        public bool TryGetCurrentStepInputValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.TryGetStepInput(StepName, key, out value);
        }

        /// <summary>
        /// Resolves a nested input value from the current step state using a dot-separated path.
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
        /// Retrieves a raw configuration value from the current step state.
        /// This method performs a direct key lookup only.
        /// </summary>
        public T? GetCurrentStepConfigValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.GetStepConfig<T>(StepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a raw configuration value from the current step state.
        /// This method performs a direct key lookup only.
        /// </summary>
        public bool TryGetCurrentStepConfigValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.TryGetStepConfig(StepName, key, out value);
        }

        /// <summary>
        /// Resolves a nested configuration value from the current step state using a dot-separated path.
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

        public T? GetStepInputValue<T>(string key) => GetCurrentStepInputValue<T>(key);

        public bool TryGetStepInputValue<T>(string key, out T? value) =>
            TryGetCurrentStepInputValue(key, out value);

        public T? GetStepConfigValue<T>(string key) => GetCurrentStepConfigValue<T>(key);

        public bool TryGetStepConfigValue<T>(string key, out T? value) =>
            TryGetCurrentStepConfigValue(key, out value);

        // ---------------------------------------------------------------------
        // STEP BINDING RESOLUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolves a named input binding from the current step state.
        ///
        /// Supported path formats:
        /// - steps.{stepName}.inputs.{path}
        /// - steps.{stepName}.config.{path}
        /// - steps.{stepName}.result.value
        /// - steps.{stepName}.result.data.{path}
        /// - state.{path}
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
        /// Retrieves a configuration value from a specific step.
        /// </summary>
        public T? GetStepConfig<T>(string stepName, string key)
        {
            return State.GetStepConfig<T>(stepName, key);
        }

        /// <summary>
        /// Attempts to retrieve a configuration value from a specific step.
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