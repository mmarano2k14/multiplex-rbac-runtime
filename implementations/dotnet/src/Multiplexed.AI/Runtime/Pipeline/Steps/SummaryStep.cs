using Multiplexed.Abstractions.AI;
using Multiplexed.Abstractions.AI.Execution;
using Multiplexed.Abstractions.AI.Execution.Context;
using Multiplexed.Abstractions.AI.Steps;
using Multiplexed.AI.Abstractions;
using Multiplexed.AI.Runtime.Execution.Context;

namespace Multiplexed.AI.Runtime.Pipeline.Steps
{
    /// <summary>
    /// Example step that summarizes input text using the AI service.
    /// </summary>
    [AiStep(stepKey: "summary")]
    public sealed class SummaryStep : IAiStep
    {
        private readonly IAiService _aiService;

        public SummaryStep(IAiService aiService)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        }

        public string Name => "summary";

        public async Task<AiStepResult> ExecuteAsync(
            AiStepExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var helper = context.GetHelper();

            // Retrieve input from context
            var input = await helper.GetDataAsync<string>(
                AiExecutionKeys.Input,
                cancellationToken).ConfigureAwait(false);

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
                data: helper.ToDictionary(new
                {
                    input = response.Content
                }));
        }
    }
}