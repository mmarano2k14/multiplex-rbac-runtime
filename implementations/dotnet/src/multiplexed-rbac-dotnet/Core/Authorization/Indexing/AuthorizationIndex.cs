using MultiplexedRbac.Core.Authorization.Scope;

namespace MultiplexedRbac.Core.Authorization.Indexing
{
    public static class AuthorizationIndex
    {
        public static void EnsureBuilt(ExecutionContext.ExecutionContext ctx, AuthorizationScope scope)
        {
            if (scope.Indexed) return;
            if (ctx is null) throw new ArgumentNullException(nameof(ctx));

            var permissionSet = ctx.Namespaces.FirstOrDefault(n => n.Name == ctx.CurrentNamespace);
            if (permissionSet is null)
                throw new InvalidOperationException(
                    $"Cannot find Permission Set for namespace: '{ctx.CurrentNamespace}'");

            foreach (var raw in permissionSet.Trns)
            {
                // trn:{project}:{ns}:{resource}:{feature}:{action}
                var parts = raw.Split(':');
                if (parts.Length != 6) continue;
                if (!parts[0].Equals("trn", StringComparison.OrdinalIgnoreCase)) continue;

                var resource = Canon(parts[3]);
                var feature = Canon(parts[4]);
                var action = Canon(parts[5]);

                // Reject partial wildcards (fail closed)
                if (!IsSegmentValid(resource) || !IsSegmentValid(feature) || !IsSegmentValid(action))
                    continue;

                // Allowed wildcard patterns:
                // *:*:*  => AllowAll
                if (resource == "*" && feature == "*" && action == "*")
                {
                    scope.AllowAll = true;
                    continue;
                }

                // *:*:a => namespace action (read-only global etc.)
                if (resource == "*" && feature == "*" && action != "*")
                {
                    scope.AllowedActions.Add(action);
                    continue;
                }

                // r:*:* => resource-wide admin
                if (resource != "*" && feature == "*" && action == "*")
                {
                    scope.AllowedResources.Add(resource);
                    continue;
                }

                // r:*:a => resource any feature, action exact
                if (resource != "*" && feature == "*" && action != "*")
                {
                    scope.AllowedResourceActions.Add((resource, action));
                    continue;
                }

                // r:f:* => feature-wide admin
                if (resource != "*" && feature != "*" && action == "*")
                {
                    scope.AllowedFeatures.Add((resource, feature));
                    continue;
                }

                // Exact r:f:a => NOT indexed here (stays exact-only)
            }

            scope.Indexed = true;
        }

        private static string Canon(string s) => (s ?? "").Trim().ToLowerInvariant();

        private static bool IsSegmentValid(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            // "*" must be full segment only
            return s == "*" || !s.Contains('*');
        }
    }
}