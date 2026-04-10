using System.Collections.Generic;

namespace Multiplexed.Abstractions.AI.Prompt
{
    /// <summary>
    /// Renders a prompt template into a final provider-ready prompt string.
    ///
    /// The template renderer is responsible only for template expansion.
    /// It does not call providers, parse responses, or apply retry policies.
    /// </summary>
    public interface IAiPromptTemplateRenderer
    {
        /// <summary>
        /// Renders the specified prompt template using the provided variables.
        /// </summary>
        /// <param name="template">
        /// The prompt template to render.
        /// </param>
        /// <param name="variables">
        /// The variables available to the template during rendering.
        /// </param>
        /// <returns>
        /// The rendered prompt string.
        /// </returns>
        string Render(
            string template,
            IReadOnlyDictionary<string, object?> variables);
    }
}