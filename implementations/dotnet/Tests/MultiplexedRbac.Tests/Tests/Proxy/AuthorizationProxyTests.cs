using MultiplexedRbac.Core.Authorization.Attributes;
using MultiplexedRbac.Core.Authorization.Engine;
using MultiplexedRbac.Core.Authorization.Proxy;
using Xunit;

namespace MultiplexedRbac.Tests.Proxy
{
    public sealed class AuthorizationProxyTests
    {
        private sealed class FakeAuth : IAuthorizationEngine
        {
            private readonly HashSet<(string r, string f, string a)> _allowed;

            public FakeAuth(params (string r, string f, string a)[] allowed)
                => _allowed = allowed.ToHashSet();

            public bool IsAllowed(string resource, string feature, string action)
                => _allowed.Contains((resource, feature, action));

            public bool IsAllowedResource(string resource) => true;
            public bool IsAllowedFeature(string resource, string feature) => true;
            public bool HasAnyAction(string action) => true;
        }

        public interface IInvoiceService
        {
            [RequireCapability("invoice", "refund", "admin")]
            Task RefundAsync(string invoiceId);
        }

        [RequireCapability("invoice", "base", "read")]
        private sealed class InvoiceService : IInvoiceService
        {
            [RequireCapability("invoice", "refund", "admin")]
            public Task RefundAsync(string invoiceId)
                => Task.CompletedTask;
        }

        [Fact]
        public async Task Proxy_Allows_When_All_Requirements_Met()
        {
            var auth = new FakeAuth(
                ("invoice", "base", "read"),
                ("invoice", "refund", "admin")
            );

            IInvoiceService svc =
                AuthorizationProxyFactory.Create<IInvoiceService>(
                    new InvoiceService(),
                    auth);

            await svc.RefundAsync("x");
        }

        [Fact]
        public async Task Proxy_Denies_When_Requirement_Missing()
        {
            var auth = new FakeAuth(
                ("invoice", "refund", "admin") // missing base/read
            );

            IInvoiceService svc =
                AuthorizationProxyFactory.Create<IInvoiceService>(
                    new InvoiceService(),
                    auth);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => svc.RefundAsync("x"));
        }
    }
}
