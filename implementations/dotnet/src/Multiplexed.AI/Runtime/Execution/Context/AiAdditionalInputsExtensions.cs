using Multiplexed.Abstractions.AI.Execution.Context;
using System.Globalization;

namespace Multiplexed.AI.Runtime.Execution.Context
{
    /// <summary>
    /// Provides typed access helpers for additional inputs.
    ///
    /// PURPOSE:
    /// - Keeps DTOs pure (data only).
    /// - Centralizes conversion and validation logic.
    /// </summary>
    public static class AiAdditionalInputsExtensions
    {
        /// <summary>
        /// Gets an optional additional input converted to the requested type.
        /// </summary>
        public static T? GetAdditional<T>(
            this IAiAdditionalInputsContainer container,
            string key)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!container.AdditionalInputs.TryGetValue(key, out var value) || value is null)
            {
                return default;
            }

            if (value is T typed)
            {
                return typed;
            }

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            if (targetType == typeof(string))
            {
                return (T?)(object?)value.ToString();
            }

            return (T?)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets a required additional input converted to the requested type.
        /// </summary>
        public static T GetRequiredAdditional<T>(
            this IAiAdditionalInputsContainer container,
            string key)
        {
            var value = container.GetAdditional<T>(key);

            if (value is null)
            {
                throw new InvalidOperationException(
                    $"Required additional input '{key}' is missing or could not be converted to '{typeof(T).Name}'.");
            }

            return value;
        }
    }
}