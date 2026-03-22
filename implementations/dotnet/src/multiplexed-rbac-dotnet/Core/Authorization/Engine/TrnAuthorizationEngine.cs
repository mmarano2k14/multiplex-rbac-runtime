using MultiplexedRbac.Core.Authorization.Indexing;
using MultiplexedRbac.Core.Authorization.Scope;
using MultiplexedRbac.Core.Authorization.Trn;
using MultiplexedRbac.Core.ExecutionContext;

namespace MultiplexedRbac.Core.Authorization.Engine
{
    /// <summary>
    /// Deterministic TRN Authorization Engine.
    /// 
    /// Part 4:
    /// - Exact TRN matching (resource, feature, action)
    /// 
    /// Part 5:
    /// - Hierarchical resolution
    /// - Suffix wildcard support
    /// - O(1) evaluation via pre-built scope index
    /// 
    /// Resolution order (most specific → least specific):
    /// 1. Exact:                r:f:a
    /// 2. Feature admin:        r:f:*
    /// 3. Resource action:      r:*:a
    /// 4. Resource admin:       r:*:*
    /// 5. Namespace action:     *:*:a
    /// 6. Namespace admin:      *:*:*
    /// 
    /// This engine is allow-only and fail-closed.
    /// </summary>
    public sealed class TrnAuthorizationEngine : IAuthorizationEngine
    {
        private readonly TrnBuilder _builder;
        private readonly AuthorizationScope _scope;
        private readonly IExecutionContextAccessor _contextAccessor;

        /// <summary>
        /// Current execution context (request-scoped).
        /// Throws if not available.
        /// </summary>
        private ExecutionContext.ExecutionContext Context
            => _contextAccessor.Current
               ?? throw new InvalidOperationException("ExecutionContext not available.");

        public TrnAuthorizationEngine(
            TrnBuilder builder,
            AuthorizationScope scope,
            IExecutionContextAccessor contextAccessor)
        {
            _builder = builder;
            _scope = scope;
            _contextAccessor = contextAccessor;
        }

        /// <summary>
        /// Performs a deterministic capability check.
        /// 
        /// The request is always concrete (no wildcards).
        /// Wildcards exist only inside the indexed scope.
        /// 
        /// Caching layers:
        /// - TRN build cache (TrnCache)
        /// - Decision cache (DecisionCache)
        /// </summary>
        public bool IsAllowed(string resource, string feature, string action)
        {
            var ctx = Context;

            // Ensure wildcard index is built once per scope
            AuthorizationIndex.EnsureBuilt(ctx, _scope);

            // Canonical normalization (strict matching)
            resource = Canon(resource);
            feature = Canon(feature);
            action = Canon(action);

            var key = (resource, feature, action);

            // Build TRN once and cache it
            if (!_scope.TrnCache.TryGetValue(key, out var trn))
            {
                trn = _builder.Build(ctx.CurrentNamespace, resource, feature, action);
                _scope.TrnCache[key] = trn;
            }

            // Decision caching (avoid recomputation)
            if (!_scope.DecisionCache.TryGetValue(trn, out var allowed))
            {
                var permissionSet = ctx.Namespaces
                    .FirstOrDefault(n => n.Name == ctx.CurrentNamespace);

                if (permissionSet is null)
                    throw new InvalidOperationException(
                        $"Cannot find Permission Set for namespace: '{ctx.CurrentNamespace}'");

                // -------------------------------
                // Part 5 Deterministic Resolution
                // -------------------------------
                allowed =
                    // 1) Exact match
                    permissionSet.Trns.Contains(trn)

                    // 2) r:f:* (feature-wide admin)
                    || _scope.AllowedFeatures.Contains((resource, feature))

                    // 3) r:*:a (any feature, action exact)
                    || _scope.AllowedResourceActions.Contains((resource, action))

                    // 4) r:*:* (resource-wide admin)
                    || _scope.AllowedResources.Contains(resource)

                    // 5) *:*:a (namespace action allow)
                    || _scope.AllowedActions.Contains(action)

                    // 6) *:*:* (namespace-wide admin)
                    || _scope.AllowAll;

                _scope.DecisionCache[trn] = allowed;
            }

            // Persist last evaluated target (observability / auditing)
            _scope.LastTarget = new AuthorizationTarget(
                ctx.CurrentNamespace,
                resource,
                feature,
                action,
                trn);

            return allowed;
        }

        /// <summary>
        /// Normalizes all segments to lower-case invariant.
        /// Ensures deterministic matching.
        /// </summary>
        private static string Canon(string s)
            => (s ?? "").Trim().ToLowerInvariant();

        /// <summary>
        /// Checks if a resource has any administrative permission.
        /// Equivalent to evaluating r:*:* or full namespace admin.
        /// </summary>
        public bool IsAllowedResource(string resource)
        {
            var ctx = Context;
            AuthorizationIndex.EnsureBuilt(ctx, _scope);

            resource = Canon(resource);

            var allowed =
                _scope.AllowAll ||
                _scope.AllowedResources.Contains(resource);

            var trn = _builder.Build(ctx.CurrentNamespace, resource, "*", "*");

            _scope.LastTarget = new AuthorizationTarget(
                ctx.CurrentNamespace,
                resource,
                "*",
                "*",
                trn);

            return allowed;
        }

        /// <summary>
        /// Checks if a specific feature under a resource is allowed
        /// (feature admin or resource/namespace admin).
        /// </summary>
        public bool IsAllowedFeature(string resource, string feature)
        {
            var ctx = Context;
            AuthorizationIndex.EnsureBuilt(ctx, _scope);

            resource = Canon(resource);
            feature = Canon(feature);

            var allowed =
                _scope.AllowAll ||
                _scope.AllowedResources.Contains(resource) ||
                _scope.AllowedFeatures.Contains((resource, feature));

            var trn = _builder.Build(ctx.CurrentNamespace, resource, feature, "*");

            _scope.LastTarget = new AuthorizationTarget(
                ctx.CurrentNamespace,
                resource,
                feature,
                "*",
                trn);

            return allowed;
        }

        /// <summary>
        /// Checks if an action is globally allowed within the namespace
        /// (e.g., read-only mode: *:*:read).
        /// </summary>
        public bool HasAnyAction(string action)
        {
            var ctx = Context;
            AuthorizationIndex.EnsureBuilt(ctx, _scope);

            action = Canon(action);

            var allowed =
                _scope.AllowAll ||
                _scope.AllowedActions.Contains(action);

            var trn = _builder.Build(ctx.CurrentNamespace, "*", "*", action);

            _scope.LastTarget = new AuthorizationTarget(
                ctx.CurrentNamespace,
                "*",
                "*",
                action,
                trn);

            return allowed;
        }
    }
}