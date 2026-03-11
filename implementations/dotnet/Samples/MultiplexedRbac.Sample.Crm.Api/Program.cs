using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Runtime;
using MultiplexedRbac.Runtime.DI;
using MultiplexedRbac.Runtime.Messaging.NServiceBus;
using MultiplexedRbac.Runtime.Messaging.NServiceBus.DI;
using MultiplexedRbac.Sample.Crm.Api.Auth;
using MultiplexedRbac.Sample.Crm.Services;
using NServiceBus;

// Alias to avoid System.ExecutionContext confusion
using ExecutionContext = MultiplexedRbac.Core.ExecutionContext.ExecutionContext;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");


// --------------------------------------------------------------------
// 1️⃣ Controllers + Swagger
// --------------------------------------------------------------------

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MultiplexedRbac.Sample.Crm.Api",
        Version = "v1",
        Description = "Part 6 — Transport-agnostic deterministic RBAC demo"
    });

    // X-Access-Context header (core to Part 3 & 6)
    c.AddSecurityDefinition("X-Access-Context", new OpenApiSecurityScheme
    {
        Name = "X-Access-Context",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "ExecutionContext handle resolved by the runtime."
    });

    // DEV-only fake user override
    c.AddSecurityDefinition("X-Demo-UserId", new OpenApiSecurityScheme
    {
        Name = "X-Demo-UserId",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "DEV only: sets authenticated user id."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "X-Access-Context"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "X-Demo-UserId"
                }
            },
            Array.Empty<string>()
        }
    });
});


// --------------------------------------------------------------------
// 2️⃣ Authentication (DEV Fake Auth)
// --------------------------------------------------------------------
// Required because ExecutionContextMiddleware denies unauthenticated users.

builder.Services
    .AddAuthentication(FakeAuthHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>(
        FakeAuthHandler.Scheme, _ => { });

builder.Services.AddAuthorization();


// --------------------------------------------------------------------
// 3️⃣ Multiplexed RBAC Runtime Registration
// --------------------------------------------------------------------
// Registers:
// - IExecutionContextAccessor
// - IContextStore (Composite)
// - AuthorizationScope
// - TrnAuthorizationEngine
// - Proxy-based dynamic registration (Part 4)
// - HTTP middleware (Part 3)
// - NServiceBus behaviors (Part 6)

builder.Services
    .AddMultiplexedRbacRuntime(builder.Configuration)
    .AddMultiplexedRbacHttp()
    .AddMultiplexedRbacNServiceBus()
    .AddCrmServices()
    .AddMultiplexedRbacAuthorizedServices(typeof(Program).Assembly);

builder.Services.AddSingleton<MultiplexedRbac.Sample.Crm.Api.Context.DemoSeedState>();

// --------------------------------------------------------------------
// 6️⃣ Cookies ticket for protection
// --------------------------------------------------------------------


builder.Services.AddDataProtection();
builder.Services.AddSingleton<IDemoBootstrapTicketProtector, DemoBootstrapTicketProtector>();


// --------------------------------------------------------------------
// 4️⃣ NServiceBus Endpoint (API acts as publisher)
// --------------------------------------------------------------------

builder.Host.UseNServiceBus(_ =>
{
    var endpointConfig = new EndpointConfiguration("MultiplexedRbac.Sample.Crm.Api");

    endpointConfig.EnableInstallers();
    endpointConfig.UseSerialization<SystemJsonSerializer>();

    var transport = endpointConfig.UseTransport<RabbitMQTransport>();
    transport.ConnectionString("host=localhost");
    transport.UseConventionalRoutingTopology(QueueType.Classic);

    // IMPORTANT:
    // Propagate X-Access-Context into outgoing message headers.
    // This proves transport-agnostic authorization.
    endpointConfig.Pipeline.Register(
        typeof(OutgoingExecutionContextHeaderBehavior),
        "Propagate X-Access-Context into outgoing NServiceBus headers.");

    return endpointConfig;
});

var app = builder.Build();


// --------------------------------------------------------------------
// 5️⃣ DEV Seed — deterministic test context
// --------------------------------------------------------------------
// This simulates a login phase (Part 1).
// In production, context would be created at authentication time.

using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IContextStore>();
    var demo = scope.ServiceProvider.GetRequiredService<MultiplexedRbac.Sample.Crm.Api.Context.DemoSeedState>();

    //var ctx = MultiplexedRbac.Sample.Crm.Api.Context.ContextFactory.Full("demo-user-1");
    //var key = await store.StoreAsync(ctx);

    //demo.AccessContextKey = key;
    //Console.WriteLine($"[SEED] Demo ContextKey: {key}");
}

// --------------------------------------------------------------------
// 6️⃣ HTTP Pipeline Ordering (CRITICAL)
// --------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();

}

//app.UseHttpsRedirection();

// 1️⃣ Authentication first
app.UseAuthentication();



app.UseWhen(ctx =>
{
    var p = ctx.Request.Path;
    return !p.StartsWithSegments("/demo")
        && !p.StartsWithSegments("/swagger")
        && !p.StartsWithSegments("/openapi");
},
branch =>
{
    // 2️⃣ Resolve + bind ExecutionContext
    branch.UseMiddleware<ExecutionContextMiddleware>();
    // 3️⃣ Enforce namespace isolation boundary
    branch.UseMiddleware<NamespaceGuardMiddleware>();
});


// 4️⃣ ASP.NET authorization layer
app.UseAuthorization();

app.MapControllers();

app.Run();