using Microsoft.Extensions.Hosting;
using NServiceBus;
using MultiplexedRbac.Runtime.DI;
using MultiplexedRbac.Runtime.Messaging.NServiceBus;
using MultiplexedRbac.Runtime.Messaging.NServiceBus.DI;
using MultiplexedRbac.Sample.Crm.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMultiplexedRbacRuntime(builder.Configuration, opt =>
    {
        opt.AccessContextHeader = "X-Access-Context";
        opt.EnableRotation = false;
    })
    .AddCrmServices()
    .AddMultiplexedRbacNServiceBus();

var endpointConfig = new EndpointConfiguration("MultiplexedRbac.Sample.Crm.Worker");
endpointConfig.SendFailedMessagesTo("MultiplexedRbac.Sample.Crm.Worker.error");
endpointConfig.AuditProcessedMessagesTo("MultiplexedRbac.Sample.Crm.Worker.audit");
endpointConfig.EnableInstallers();
endpointConfig.UseSerialization<SystemJsonSerializer>();

var transport = endpointConfig.UseTransport<RabbitMQTransport>();
transport.ConnectionString("host=localhost");
transport.UseConventionalRoutingTopology(QueueType.Classic);

// ✅ Pipeline wiring (THIS is what makes behaviors run)
endpointConfig.Pipeline.Register(
    typeof(IncomingExecutionContextRehydrateBehavior),
    "Rehydrate ExecutionContext from X-Access-Context header using IContextStore.");

endpointConfig.Pipeline.Register(
    typeof(OutgoingExecutionContextHeaderBehavior),
    "Propagate X-Access-Context into outgoing NServiceBus headers.");

builder.UseNServiceBus(endpointConfig);

var host = builder.Build();
await host.RunAsync();