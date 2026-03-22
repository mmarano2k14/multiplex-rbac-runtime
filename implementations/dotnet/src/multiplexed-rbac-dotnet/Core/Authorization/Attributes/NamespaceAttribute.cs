namespace MultiplexedRbac.Core.Authorization.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class NamespaceAttribute : Attribute
    {
        public NamespaceAttribute(string value) => Value = value;
        public string Value { get; }
    }
}
