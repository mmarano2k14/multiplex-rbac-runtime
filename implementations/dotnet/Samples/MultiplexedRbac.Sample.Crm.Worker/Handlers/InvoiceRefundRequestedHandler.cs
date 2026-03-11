using NServiceBus;
using MultiplexedRbac.Sample.Crm.Contracts;
using MultiplexedRbac.Core.Authorization.Engine;
using MultiplexedRbac.Sample.Crm.Contracts.Events;

namespace MultiplexedRbac.Sample.Crm.Worker.Handlers;

public sealed class InvoiceRefundRequestedHandler : IHandleMessages<InvoiceRefundRequested>
{
    private readonly IAuthorizationEngine _auth; // ton engine Part 4

    public InvoiceRefundRequestedHandler(IAuthorizationEngine auth)
    {
        _auth = auth;
    }

    public async Task Handle(InvoiceRefundRequested message, IMessageHandlerContext context)
    {
        // ✅ Context already rehydrated by IncomingExecutionContextRehydrateBehavior
        // So auth engine reads ExecutionContext via accessor (no ctx passing).

        if (!_auth.IsAllowed("billing", "invoice", "refund"))
        {
            await context.Publish(new InvoiceRefundDenied
            {
                InvoiceId = message.InvoiceId,
                Reason = "Forbidden (worker re-check)"
            });
            return;
        }

        // Simulate long-running work (we’ll replace by Hangfire job later)
        await Task.Delay(500, context.CancellationToken);

        await context.Publish(new InvoiceRefundProcessed
        {
            InvoiceId = message.InvoiceId,
            ProviderRef = "psp-demo-001"
        });
    }
}