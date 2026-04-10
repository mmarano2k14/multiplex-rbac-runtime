using System;
using System.Collections.Generic;
using System.Globalization;
using Multiplexed.Abstractions.AI.Prompt;

namespace Multiplexed.AI.Runtime.AI.Prompt
{
    /// <summary>
    /// Default implementation of <see cref="IAiPromptTemplateRenderer"/>.
    ///
    /// This renderer provides a simple token replacement strategy using
    /// double curly braces:
    ///
    /// Example:
    /// "Hello {{userName}}"
    ///
    /// This implementation is intentionally simple and deterministic.
    /// It can later be replaced by a more advanced templating engine if needed.
    /// </summary>
    public sealed class DefaultAiPromptTemplateRenderer : IAiPromptTemplateRenderer
    {
        /// <inheritdoc />
        public string Render(
            string template,
            IReadOnlyDictionary<string, object?> variables)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(variables);

            var rendered = template;

            foreach (var pair in variables)
            {
                var token = "{{" + pair.Key + "}}";
                var value = ConvertToString(pair.Value);

                rendered = rendered.Replace(token, value, StringComparison.Ordinal);
            }

            return rendered;
        }

        /// <summary>
        /// Converts a template variable value into a deterministic string form.
        /// </summary>
        private static string ConvertToString(object? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            return value switch
            {
                string stringValue => stringValue,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }
    }
}