namespace Multiplexed.Abstractions.AI.Steps
{
    /// <summary>
    /// Declares the unique pipeline step key used to resolve an AI step from a declarative pipeline definition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AiStepAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiStepAttribute"/> class.
        /// </summary>
        /// <param name="stepKey">The unique step key.</param>
        public AiStepAttribute(string stepKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);

            StepKey = stepKey;
        }

        /// <summary>
        /// Gets the unique step key.
        /// </summary>
        public string StepKey { get; }
    }
}