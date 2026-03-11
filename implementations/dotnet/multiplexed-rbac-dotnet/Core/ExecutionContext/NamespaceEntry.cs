namespace MultiplexedRbac.Core.ExecutionContext
{
    public class NamespaceEntry
    {
        public required string Name { get; init; }
        public required HashSet<string> Trns { get; init; }
    }
}
