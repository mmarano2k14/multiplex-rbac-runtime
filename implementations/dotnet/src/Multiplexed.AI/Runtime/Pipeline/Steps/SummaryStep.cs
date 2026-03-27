using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.AI.Runtime.Pipeline.Steps
{
    /// <summary>
    /// Example step that summarizes input text using the AI service.
    /// </summary>
    [AiStep(stepKey:"summary")]
    public sealed class SummaryStep : IAiStep
    {
        private readonly IAiService _aiService;

        public SummaryStep(IAiService aiService)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        }

        public string Name => "summary";

        public async Task<AiStepResult> ExecuteAsync(
            AiExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            // Retrieve input from context
            var input = context.Get<string>(AiExecutionKeys.Input);

            if (string.IsNullOrWhiteSpace(input))
                return AiStepResult.Fail("Missing required input: 'input'.");

            // Call AI service
            var response = await _aiService.CompleteAsync(
                new AiRequest
                {
                    Prompt = $"Summarize the following content:\n\n{input}"
                },
                cancellationToken);

            // Return result and inject into context
            return AiStepResult.Ok(
                output: response.Content,
                data: new Dictionary<string, object?>
                {
                    [AiExecutionKeys.Input] = response.Content
                });
        }
    }
}
