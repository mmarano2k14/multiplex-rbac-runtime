namespace MultiplexedRbac.Core.Authorization.Engine
{
    public interface IAuthorizationEngine
    {
        // Exact capability check
        bool IsAllowed(string resource, string feature, string action);

        // Helper levels
        bool IsAllowedResource(string resource);
        bool IsAllowedFeature(string resource, string feature);
        bool HasAnyAction(string action);
    }
}
