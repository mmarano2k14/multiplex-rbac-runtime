using Microsoft.Extensions.Options;
using MultiplexedRbac.Core.Authorization.Engine;
using MultiplexedRbac.Core.Authorization.Scope;
using MultiplexedRbac.Core.Authorization.Trn;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Runtime;
using Xunit;

namespace MultiplexedRbac.Tests.Engine
{
    public sealed class TrnAuthorizationEngineWildcardTests
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

        private static TrnAuthorizationEngine BuildEngine(params string[] trns)
        {
            var ctx = BuildCtx(trns);

            var accessor = new ExecutionContextAccessor();
            accessor.Set(ctx);

            var trnOptions = Options.Create(new TrnBuilderOptions
            {
                Project = "tev"
            });

            var trnBuilder = new TrnBuilder(trnOptions);

            return new TrnAuthorizationEngine(
                trnBuilder,
                new AuthorizationScope(),
                accessor);
        }

        [Fact]
        public void Allows_FeatureAdmin_r_f_star()
        {
            var engine = BuildEngine("trn:tev:crm:billing:invoice:*");

            Assert.True(engine.IsAllowed("billing", "invoice", "read"));
            Assert.True(engine.IsAllowed("billing", "invoice", "write"));
            Assert.True(engine.IsAllowed("billing", "invoice", "delete"));
        }

        [Fact]
        public void Allows_ResourceAction_r_star_a()
        {
            var engine = BuildEngine("trn:tev:crm:billing:*:refund");

            Assert.True(engine.IsAllowed("billing", "invoice", "refund"));
            Assert.True(engine.IsAllowed("billing", "payment", "refund"));
            Assert.False(engine.IsAllowed("billing", "invoice", "write"));
        }

        [Fact]
        public void Allows_ResourceAdmin_r_star_star()
        {
            var engine = BuildEngine("trn:tev:crm:billing:*:*");

            Assert.True(engine.IsAllowed("billing", "invoice", "read"));
            Assert.True(engine.IsAllowed("billing", "invoice", "refund"));
            Assert.True(engine.IsAllowed("billing", "payment", "delete"));
        }

        [Fact]
        public void Allows_NamespaceAction_star_star_a()
        {
            var engine = BuildEngine("trn:tev:crm:*:*:read");

            Assert.True(engine.IsAllowed("billing", "invoice", "read"));
            Assert.True(engine.IsAllowed("candidate", "pipeline", "read"));

            Assert.False(engine.IsAllowed("billing", "invoice", "write"));
            Assert.False(engine.IsAllowed("billing", "invoice", "delete"));
        }

        [Fact]
        public void Allows_NamespaceAdmin_star_star_star()
        {
            var engine = BuildEngine("trn:tev:crm:*:*:*");

            Assert.True(engine.IsAllowed("billing", "invoice", "read"));
            Assert.True(engine.IsAllowed("billing", "invoice", "write"));
            Assert.True(engine.IsAllowed("candidate", "pipeline", "delete"));
        }

        [Fact]
        public void Rejects_InvalidPattern_star_feature_action()
        {
            // This pattern is intentionally unsupported: *:invoice:read
            // It must not grant anything (fail closed).
            var engine = BuildEngine("trn:tev:crm:*:invoice:read");

            Assert.False(engine.IsAllowed("billing", "invoice", "read"));
            Assert.False(engine.IsAllowed("candidate", "invoice", "read"));
        }

        [Fact]
        public void Precedence_Exact_Beats_Wildcards()
        {
            // Exact allow exists even if read-only global exists
            var engine = BuildEngine(
                "trn:tev:crm:*:*:read",
                "trn:tev:crm:billing:invoice:delete"
            );

            Assert.True(engine.IsAllowed("billing", "invoice", "read"));
            Assert.True(engine.IsAllowed("billing", "invoice", "delete")); // exact
            Assert.False(engine.IsAllowed("billing", "invoice", "write")); // not granted
        }
    }
}