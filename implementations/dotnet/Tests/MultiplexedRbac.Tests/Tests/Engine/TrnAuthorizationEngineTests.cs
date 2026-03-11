using Microsoft.Extensions.Options;
using MultiplexedRbac.Core.Authorization.Engine;
using MultiplexedRbac.Core.Authorization.Scope;
using MultiplexedRbac.Core.Authorization.Trn;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Runtime;
using Xunit;

namespace MultiplexedRbac.Tests.Engine
{
    public sealed class TrnAuthorizationEngineTests
    {
        private static Core.ExecutionContext.ExecutionContext BuildCtx(params string[] trns)
        {
            return new Core.ExecutionContext.ExecutionContext
            {
                ContextKey = "context-key-xxx",
                Project = "tev",
                UserId = "user-xxx",
                TenantId = "tenant-id-xxx",
                TenantGroupId = "tenant-group-id-xxx",
                CurrentNamespace = "crm",
                Namespaces = new List<NamespaceEntry>
                {
                    new NamespaceEntry
                    {
                        Name = "crm",
                        Trns = trns.ToHashSet()
                    }
                }
            };
        }

        [Fact]
        public async Task IsAllowed_ReturnsTrue_WhenExactTrnExists()
        {
            // Exact TRN (Part 4 behavior remains valid in Part 5)
            var ctx = BuildCtx("trn:tev:crm:invoice:refund:read");

            var accessor = new ExecutionContextAccessor();
            accessor.Set(ctx);

            var scope = new AuthorizationScope();

            var trnOptions = Options.Create(new TrnBuilderOptions
            {
                Project = "tev"
            });

            var trnBuilder = new TrnBuilder(trnOptions);

            var engine = new TrnAuthorizationEngine(
                trnBuilder,
                scope,
                accessor);

            await Task.Yield();

            Assert.True(engine.IsAllowed("invoice", "refund", "read"));
            Assert.False(engine.IsAllowed("invoice", "refund", "write"));
        }

        [Fact]
        public async Task HelperIndexes_Work_WithWildcardsOnly()
        {
            // IMPORTANT:
            // After Part 5, the index is built ONLY from wildcard TRNs.
            // Exact TRNs do not populate AllowedResources / AllowedFeatures / AllowedActions.

            var ctx = BuildCtx(
                "trn:tev:crm:invoice:*:*",          // resource admin (r:*:*)
                "trn:tev:crm:candidate:pipeline:*", // feature admin (r:f:*)
                "trn:tev:crm:*:*:read"              // namespace action (*:*:a)
            );

            var accessor = new ExecutionContextAccessor();
            accessor.Set(ctx);
            var scope = new AuthorizationScope();

            var trnOptions = Options.Create(new TrnBuilderOptions
            {
                Project = "tev"
            });

            var trnBuilder = new TrnBuilder(trnOptions);

            var engine = new TrnAuthorizationEngine(
                trnBuilder,
                scope,
                accessor);

            await Task.Yield();

            Assert.True(engine.IsAllowedResource("invoice"));
            Assert.True(engine.IsAllowedFeature("candidate", "pipeline"));
            Assert.True(engine.HasAnyAction("read"));

            // Sanity check: write should NOT be granted by *:*:read
            Assert.False(engine.HasAnyAction("write"));
        }
    }
}