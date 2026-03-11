using MultiplexedRbac.Sample.Crm.Services.Contracts;

namespace MultiplexedRbac.Sample.Crm.Services
{
    public sealed class BillingAdminService : IBillingAdminService
    {
        public Task RecomputeLedgerAsync(CancellationToken ct = default)
            => Task.Delay(80, ct);

        public Task PurgeTestDataAsync(CancellationToken ct = default)
            => Task.Delay(80, ct);
    }
}
