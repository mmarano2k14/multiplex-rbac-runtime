namespace MultiplexedRbac.Core.Authorization.Attributes
{
    [AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface,
    AllowMultiple = true,
    Inherited = true)]
    public sealed class RequireCapabilityAttribute : Attribute
    {
        public string Resource { get; }
        public string Feature { get; }
        public string Action { get; }

        public RequireCapabilityAttribute(string resource, string feature, string action)
        {
            if (string.IsNullOrWhiteSpace(resource))
                throw new ArgumentException("Resource cannot be null or empty.", nameof(resource));

            if (string.IsNullOrWhiteSpace(feature))
                throw new ArgumentException("Feature cannot be null or empty.", nameof(feature));

            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Action cannot be null or empty.", nameof(action));

            Resource = resource.Trim();
            Feature = feature.Trim();
            Action = action.Trim();
        }
    }
}
