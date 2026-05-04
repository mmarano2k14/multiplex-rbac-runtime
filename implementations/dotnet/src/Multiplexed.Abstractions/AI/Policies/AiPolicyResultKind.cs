namespace Multiplexed.AI.Abstractions.AI.Policies
{
    /// <summary>
    /// Represents the kind of a policy execution result.
    /// </summary>
    public enum AiPolicyResultKind
    {
        Success = 0,
        Warning = 1,
        Block = 2,
        Retry = 3
    }
}