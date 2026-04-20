using Multiplexed.Abstractions.AI.Execution;

namespace Multiplexed.AI.Runtime.Execution.Normalization
{
    /// <summary>
    /// Default implementation of <see cref="IAiStepResultNormalizerPipeline"/>.
    ///
    /// PURPOSE:
    /// - Applies all registered step result normalizers in order.
    /// - Keeps the execution engine decoupled from module-specific normalization details.
    ///
    /// DESIGN:
    /// - The pipeline is intentionally simple.
    /// - Each normalizer is responsible for its own domain.
    /// - The pipeline itself contains no domain-specific logic.
    ///
    /// IMPORTANT:
    /// - Normalizers should be idempotent.
    /// - Execution state may already be partially or fully normalized.
    /// </summary>
    public sealed class DefaultAiStepResultNormalizerPipeline : IAiStepResultNormalizerPipeline
    {
        private readonly IReadOnlyList<IAiStepResultNormalizer> _normalizers;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAiStepResultNormalizerPipeline"/> class.
        /// </summary>
        /// <param name="normalizers">
        /// The registered step result normalizers.
        /// </param>
        public DefaultAiStepResultNormalizerPipeline(
            IEnumerable<IAiStepResultNormalizer> normalizers)
        {
            ArgumentNullException.ThrowIfNull(normalizers);

            _normalizers = normalizers.ToArray();
        }

        /// <inheritdoc />
        public void Normalize(AiExecutionState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            foreach (var normalizer in _normalizers)
            {
                normalizer.Normalize(state);
            }
        }
    }
}