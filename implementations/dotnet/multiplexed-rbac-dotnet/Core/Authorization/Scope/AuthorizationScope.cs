namespace MultiplexedRbac.Core.Authorization.Scope
{
    public sealed class AuthorizationScope
    {
        public AuthorizationTarget? LastTarget { get; set; }

        // Existing caches (keep)
        public Dictionary<(string Resource, string Feature, string Action), string> TrnCache { get; } = new();
        public Dictionary<string, bool> DecisionCache { get; } = new();
        public bool Indexed { get; set; } = false;

        // Existing indexes (keep)
        // resource:*:*  (resource-wide admin)
        public HashSet<string> AllowedResources { get; } = new();

        // resource:feature:* (feature-wide admin)
        public HashSet<(string Resource, string Feature)> AllowedFeatures { get; } = new();

        // *:*:action  (namespace action allow, e.g. read-only)
        public HashSet<string> AllowedActions { get; } = new();

        // ✅ NEW: resource:*:action  (any feature, action exact)
        public HashSet<(string Resource, string Action)> AllowedResourceActions { get; } = new();

        // ✅ NEW: *:*:*  (full namespace admin)
        public bool AllowAll { get; set; } = false;
    }

    public readonly record struct AuthorizationTarget(
        string Namespace,
        string Resource,
        string Feature,
        string Action,
        string Trn
    );
}
