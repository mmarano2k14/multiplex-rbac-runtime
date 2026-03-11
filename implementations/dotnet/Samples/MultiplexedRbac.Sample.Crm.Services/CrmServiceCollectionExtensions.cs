using Microsoft.Extensions.DependencyInjection;
using MultiplexedRbac.Sample.Crm.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiplexedRbac.Sample.Crm.Services
{
    public static class CrmServiceCollectionExtensions
    {
        public static IServiceCollection AddCrmServices(this IServiceCollection services)
        {
            // IMPORTANT:
            // Register concrete classes. Your AddAuthorizedServices(...) will wrap interfaces with proxies.
            services.AddScoped<InvoiceService>();
            services.AddScoped<IInvoiceService>(sp => sp.GetRequiredService<InvoiceService>());

            services.AddScoped<BillingAdminService>();
            services.AddScoped<IBillingAdminService>(sp => sp.GetRequiredService<BillingAdminService>());

            services.AddScoped<AuditService>(); // class-level guard example
            services.AddScoped<IAuditService>(sp => sp.GetRequiredService<AuditService>());

            return services;
        }
    }
}
