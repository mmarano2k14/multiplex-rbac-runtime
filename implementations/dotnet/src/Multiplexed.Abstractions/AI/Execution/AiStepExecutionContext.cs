using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Pipeline;
using System.Globalization;
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
        private Dictionary<string, object?>? _declaredInputsCache;
        private Dictionary<string, object?>? _declaredInputsWithReservedCache;

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

            Execution.State.EnsureStepInitialized(step);
        }

        public AiExecutionContext Execution { get; }

        public ResolvedAiPipelineStep Step { get; }

        public AiExecutionRecord Record => Execution.Record;

        public AiExecutionState State => Execution.State;

        public IServiceProvider Services => Execution.Services;

        public CancellationToken CancellationToken => Execution.CancellationToken;

        public string ExecutionId => Execution.ExecutionId;

        public string StepName => Step.Name;

        public string StepKey => Step.StepKey;

        public AiStepState StepState => State.GetOrCreateStep(StepName);

        // ---------------------------------------------------------------------
        // STEP INPUT ACCESS
        // ---------------------------------------------------------------------

        public T? GetCurrentStepInputValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.GetStepInput<T>(StepName, key);
        }

        public bool TryGetCurrentStepInputValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.TryGetStepInput(StepName, key, out value);
        }

        public T? ResolveCurrentStepInput<T>(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return TryResolveNestedValue(StepState.Inputs, path, out T? value)
                ? value
                : default;
        }

        public bool TryResolveCurrentStepInput<T>(string path, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return TryResolveNestedValue(StepState.Inputs, path, out value);
        }

        // ---------------------------------------------------------------------
        // STEP CONFIG ACCESS
        // ---------------------------------------------------------------------

        public T? GetCurrentStepConfigValue<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.GetStepConfig<T>(StepName, key);
        }

        public bool TryGetCurrentStepConfigValue<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return State.TryGetStepConfig(StepName, key, out value);
        }

        public T? ResolveCurrentStepConfig<T>(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return TryResolveNestedValue(StepState.Config, path, out T? value)
                ? value
                : default;
        }

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

        public T? ResolveInputBinding<T>(string inputName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputName);

            if (!State.TryGetStepInput(StepName, inputName, out string? path))
                return default;

            if (string.IsNullOrWhiteSpace(path))
                return default;

            return ResolvePath<T>(path);
        }

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

        public T? ResolveConfigBinding<T>(string configName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configName);

            if (!State.TryGetStepConfig(StepName, configName, out string? path))
                return default;

            if (string.IsNullOrWhiteSpace(path))
                return default;

            return ResolvePath<T>(path);
        }

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
        // DECLARED INPUT RESOLUTION
        // ---------------------------------------------------------------------

        /// <summary>
        /// Builds or returns a cached flat variable bag from the current step declared inputs.
        ///
        /// BEHAVIOR:
        /// - The dictionary is composed once, then cached for subsequent access
        /// - The version with reserved variables is cached separately
        /// - Callers can safely use this as the shared variable bag for the whole step execution
        /// </summary>
        public Dictionary<string, object?> ResolveDeclaredInputs(bool includeReservedVariables = false)
        {
            if (includeReservedVariables)
            {
                if (_declaredInputsWithReservedCache is not null)
                {
                    return _declaredInputsWithReservedCache;
                }

                _declaredInputsWithReservedCache = BuildDeclaredInputs(includeReservedVariables: true);
                return _declaredInputsWithReservedCache;
            }

            if (_declaredInputsCache is not null)
            {
                return _declaredInputsCache;
            }

            _declaredInputsCache = BuildDeclaredInputs(includeReservedVariables: false);
            return _declaredInputsCache;
        }

        /// <summary>
        /// Gets one resolved variable from the composed declared input bag.
        /// </summary>
        public T? GetVariable<T>(string variableName, bool includeReservedVariables = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(variableName);

            return TryGetVariable(variableName, out T? value, includeReservedVariables)
                ? value
                : default;
        }

        /// <summary>
        /// Gets one required resolved variable from the composed declared input bag.
        /// </summary>
        public T GetRequiredVariable<T>(string variableName, bool includeReservedVariables = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(variableName);

            if (!TryGetVariable(variableName, out T? value, includeReservedVariables) || value is null)
            {
                throw new InvalidOperationException(
                    $"Required variable '{variableName}' is missing or invalid for step '{StepName}'.");
            }

            return value;
        }

        /// <summary>
        /// Attempts to retrieve one resolved variable from the composed declared input bag.
        /// </summary>
        public bool TryGetVariable<T>(
            string variableName,
            out T? value,
            bool includeReservedVariables = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(variableName);

            var variables = ResolveDeclaredInputs(includeReservedVariables);

            if (!variables.TryGetValue(variableName, out var rawValue))
            {
                value = default;
                return false;
            }

            return TryConvertValue(rawValue, variableName, out value);
        }

        /// <summary>
        /// Resolves one declared input entry from the current step and returns the raw value.
        /// </summary>
        public bool TryResolveDeclaredInput(
            string variableName,
            object? inputDefinition,
            out object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(variableName);

            var path = ExtractStringValue(inputDefinition);

            if (!string.IsNullOrWhiteSpace(path))
            {
                if (TryResolvePath<object?>(path, out var resolvedValue))
                {
                    value = resolvedValue;
                    return true;
                }
            }

            if (TryConvertDirectValue(inputDefinition, out var directValue))
            {
                value = directValue;
                return true;
            }

            if (TryResolveInputBinding<object?>(variableName, out var boundValue))
            {
                value = boundValue;
                return true;
            }

            if (TryResolveCurrentStepInput<object?>(variableName, out var stepValue))
            {
                value = stepValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Resolves one declared input entry from the current step and converts it to the requested type.
        /// </summary>
        public T? ResolveDeclaredInput<T>(string variableName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(variableName);

            if (!TryResolveDeclaredInput(variableName, out T? value))
            {
                return default;
            }

            return value;
        }

        /// <summary>
        /// Resolves one declared input entry from the current step and converts it to the requested type.
        /// </summary>
        public bool TryResolveDeclaredInput<T>(string variableName, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(variableName);

            if (StepState.Inputs is null ||
                !StepState.Inputs.TryGetValue(variableName, out var inputDefinition))
            {
                value = default;
                return false;
            }

            if (!TryResolveDeclaredInput(variableName, inputDefinition, out var rawValue))
            {
                value = default;
                return false;
            }

            return TryConvertValue(rawValue, variableName, out value);
        }

        // ---------------------------------------------------------------------
        // CROSS-STEP / GLOBAL PATH RESOLUTION
        // ---------------------------------------------------------------------

        public T? ResolvePath<T>(string path)
        {
            return Execution.ResolvePath<T>(path);
        }

        public bool TryResolvePath<T>(string path, out T? value)
        {
            return Execution.TryResolvePath(path, out value);
        }

        public T? GetStepConfig<T>(string stepName, string key)
        {
            return State.GetStepConfig<T>(stepName, key);
        }

        public bool TryGetStepConfig<T>(string stepName, string key, out T? value)
        {
            return State.TryGetStepConfig(stepName, key, out value);
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return Execution.GetRequiredService<T>();
        }

        // ---------------------------------------------------------------------
        // INTERNAL HELPERS
        // ---------------------------------------------------------------------

        private Dictionary<string, object?> BuildDeclaredInputs(bool includeReservedVariables)
        {
            var variables = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (StepState.Inputs is not null)
            {
                foreach (var entry in StepState.Inputs)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    if (TryResolveDeclaredInput(entry.Key, entry.Value, out var resolvedValue))
                    {
                        variables[entry.Key] = resolvedValue;
                    }
                }
            }

            if (includeReservedVariables)
            {
                variables["executionId"] = ExecutionId;
                variables["stepName"] = StepName;
                variables["stepKey"] = StepKey;
                variables["currentStep"] = StepName;
                variables["currentStepKey"] = StepKey;
            }

            return variables;
        }

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
                value = (T?)Convert.ChangeType(current, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        private static string? ExtractStringValue(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is string text)
            {
                return text;
            }

            if (value is JsonElement jsonElement &&
                jsonElement.ValueKind == JsonValueKind.String)
            {
                return jsonElement.GetString();
            }

            return null;
        }

        private static bool TryConvertDirectValue(
            object? value,
            out object? directValue)
        {
            if (value is null)
            {
                directValue = null;
                return false;
            }

            if (value is JsonElement jsonElement)
            {
                directValue = ConvertJsonElement(jsonElement);
                return true;
            }

            directValue = value;
            return true;
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var intValue))
                    {
                        return intValue;
                    }

                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return doubleValue;
                    }

                    return element.GetRawText();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;

                default:
                    return element.GetRawText();
            }
        }

        /// <summary>
        /// Converts a raw resolved value into the requested target type.
        /// </summary>
        private static bool TryConvertValue<T>(
            object? value,
            string inputName,
            out T? convertedValue)
        {
            if (value is null)
            {
                convertedValue = default;
                return false;
            }

            if (value is T typed)
            {
                convertedValue = typed;
                return true;
            }

            if (value is JsonElement jsonElement)
            {
                try
                {
                    convertedValue = ConvertJsonElementTo<T>(jsonElement, inputName);
                    return true;
                }
                catch
                {
                    convertedValue = default;
                    return false;
                }
            }

            try
            {
                convertedValue = (T?)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                convertedValue = default;
                return false;
            }
        }

        /// <summary>
        /// Converts a JsonElement into the requested target type.
        /// </summary>
        private static T? ConvertJsonElementTo<T>(JsonElement element, string inputName)
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (targetType == typeof(int))
            {
                object converted = ConvertJsonElementToInt(element, inputName);
                return (T)converted;
            }

            if (targetType == typeof(long))
            {
                object converted = ConvertJsonElementToLong(element, inputName);
                return (T)converted;
            }

            if (targetType == typeof(double))
            {
                object converted = ConvertJsonElementToDouble(element, inputName);
                return (T)converted;
            }

            if (targetType == typeof(decimal))
            {
                object converted = ConvertJsonElementToDecimal(element, inputName);
                return (T)converted;
            }

            if (targetType == typeof(string))
            {
                object? converted = element.ValueKind == JsonValueKind.String
                    ? element.GetString()
                    : element.GetRawText();

                return (T?)converted;
            }

            return element.Deserialize<T>();
        }

        private static int ConvertJsonElementToInt(JsonElement element, string inputName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intValue))
                    {
                        return intValue;
                    }

                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return (int)Math.Round(doubleValue, MidpointRounding.AwayFromZero);
                    }

                    break;

                case JsonValueKind.String:
                    var text = element.GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                        {
                            return parsedInt;
                        }

                        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                        {
                            return (int)Math.Round(parsedDouble, MidpointRounding.AwayFromZero);
                        }
                    }

                    break;
            }

            throw new InvalidOperationException(
                $"Input '{inputName}' JSON value could not be converted to an integer. JsonValueKind: '{element.ValueKind}'.");
        }

        private static long ConvertJsonElementToLong(JsonElement element, string inputName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var longValue))
                    {
                        return longValue;
                    }

                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return (long)Math.Round(doubleValue, MidpointRounding.AwayFromZero);
                    }

                    break;

                case JsonValueKind.String:
                    var text = element.GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
                        {
                            return parsedLong;
                        }

                        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                        {
                            return (long)Math.Round(parsedDouble, MidpointRounding.AwayFromZero);
                        }
                    }

                    break;
            }

            throw new InvalidOperationException(
                $"Input '{inputName}' JSON value could not be converted to a long. JsonValueKind: '{element.ValueKind}'.");
        }

        private static double ConvertJsonElementToDouble(JsonElement element, string inputName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return doubleValue;
                    }

                    break;

                case JsonValueKind.String:
                    var text = element.GetString();

                    if (!string.IsNullOrWhiteSpace(text) &&
                        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                    {
                        return parsedDouble;
                    }

                    break;
            }

            throw new InvalidOperationException(
                $"Input '{inputName}' JSON value could not be converted to a double. JsonValueKind: '{element.ValueKind}'.");
        }

        private static decimal ConvertJsonElementToDecimal(JsonElement element, string inputName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetDecimal(out var decimalValue))
                    {
                        return decimalValue;
                    }

                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
                    }

                    break;

                case JsonValueKind.String:
                    var text = element.GetString();

                    if (!string.IsNullOrWhiteSpace(text) &&
                        decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDecimal))
                    {
                        return parsedDecimal;
                    }

                    break;
            }

            throw new InvalidOperationException(
                $"Input '{inputName}' JSON value could not be converted to a decimal. JsonValueKind: '{element.ValueKind}'.");
        }
    }
}