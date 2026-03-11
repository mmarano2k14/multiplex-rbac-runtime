using MultiplexedRbac.Core.Authorization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiplexedRbac.Sample.Crm.Services.Contracts
{
    [RequireCapability("billing", "admin", "access")]
    public interface IBillingAdminService
    {
        Task RecomputeLedgerAsync(CancellationToken ct = default);

        // Method-level: even stronger
        [RequireCapability("billing", "admin", "purge")]
        Task PurgeTestDataAsync(CancellationToken ct = default);
    }
}
