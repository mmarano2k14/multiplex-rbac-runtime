using Multiplexed.Abstractions.AI.Execution.Context;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Multiplexed.AI.Runtime.Execution.Context
{
    /// <summary>
    /// Default implementation of IAiStepArguments.
    ///
    /// PURPOSE:
    /// - Wraps a resolved dictionary of step arguments.
    /// - Supports typed access through Get, GetRequired, and TryGet.
    /// - Supports DTO binding through Bind and BindWithExtras.
    /// </summary>
    public sealed class AiStepArguments : IAiStepArguments
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly Dictionary<string, object?> _values;

        /// <summary>
        /// Initializes a new instance of the argument bag.
        /// </summary>
        public AiStepArguments(Dictionary<string, object?> values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>
        /// Gets the resolved argument values.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Values => _values;

        /// <summary>
        /// Gets an optional argument value converted to the requested type.
        /// </summary>
        public T? Get<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return TryGet<T>(key, out var value)
                ? value
                : default;
        }

        /// <summary>
        /// Gets a required argument value converted to the requested type.
        /// </summary>
        public T GetRequired<T>(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!TryGet<T>(key, out var value) || value is null)
            {
                throw new InvalidOperationException(
                    $"Required argument '{key}' is missing or could not be converted to '{typeof(T).Name}'.");
            }

            return value;
        }

        /// <summary>
        /// Attempts to get an argument value converted to the requested type.
        /// </summary>
        public bool TryGet<T>(string key, out T? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!_values.TryGetValue(key, out var raw) || raw is null)
            {
                value = default;
                return false;
            }

            try
            {
                value = ConvertValue<T>(raw);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Returns a mutable dictionary copy of the resolved arguments.
        /// </summary>
        public Dictionary<string, object?> ToDictionary()
        {
            return new Dictionary<string, object?>(_values, StringComparer.Ordinal);
        }

        /// <summary>
        /// Binds the resolved arguments to a strongly typed DTO.
        /// </summary>
        public T Bind<T>() where T : class, new()
        {
            var json = JsonSerializer.Serialize(_values, JsonOptions);

            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new InvalidOperationException(
                       $"Unable to bind arguments to DTO '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Binds the resolved arguments to a strongly typed DTO and preserves
        /// unmapped values when the DTO implements IAiAdditionalInputsContainer.
        /// </summary>
        public T BindWithExtras<T>() where T : class, new()
        {
            var instance = Bind<T>();

            if (instance is not IAiAdditionalInputsContainer container)
            {
                return instance;
            }

            var mappedPropertyNames = typeof(T)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanWrite || property.CanRead)
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _values)
            {
                if (!mappedPropertyNames.Contains(entry.Key))
                {
                    container.AdditionalInputs[entry.Key] = entry.Value;
                }
            }

            return instance;
        }

        /// <summary>
        /// Converts one raw argument value to the requested type.
        /// </summary>
        private static T? ConvertValue<T>(object value)
        {
            if (value is T typed)
            {
                return typed;
            }

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (value is JsonElement json)
            {
                return json.Deserialize<T>(JsonOptions);
            }

            if (targetType.IsEnum)
            {
                if (value is string enumText &&
                    Enum.TryParse(targetType, enumText, ignoreCase: true, out var enumValue))
                {
                    return (T?)enumValue;
                }

                return (T?)Enum.ToObject(targetType, value);
            }

            if (targetType == typeof(string))
            {
                return (T?)(object?)value.ToString();
            }

            if (IsSimpleConvertibleType(targetType))
            {
                return (T?)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            var jsonText = JsonSerializer.Serialize(value, JsonOptions);
            return JsonSerializer.Deserialize<T>(jsonText, JsonOptions);
        }

        /// <summary>
        /// Determines whether a type can safely use Convert.ChangeType.
        /// </summary>
        private static bool IsSimpleConvertibleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            return type.IsPrimitive ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(Guid) ||
                   type == typeof(TimeSpan);
        }
    }
}