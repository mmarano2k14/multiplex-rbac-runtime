using Microsoft.Extensions.Caching.Memory;
using MultiplexedRbac.Runtime;
using MultiplexedRbac.Stores.Cache;
using MultiplexedRbac.Stores.Memory;
using MultiplexedRbac.Stores;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MultiplexedRbac.Core.ExecutionContext;
using MultiplexedRbac.Core.Authorization.Engine;
using MultiplexedRbac.Core.Authorization.Scope;
using MultiplexedRbac.Core.Authorization.Registration;


var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------------------------
// 1. MVC / Controllers
// --------------------------------------------------------------------
builder.Services.AddControllers();


// --------------------------------------------------------------------
// 2. Authentication (JWT only — no authorization yet)
// --------------------------------------------------------------------
// JWT proves identity.
// Authorization will be layered later (Part 4).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Configure Authority / Audience / Validation parameters here
        // options.Authority = "...";
        // options.Audience = "...";
    });


// --------------------------------------------------------------------
// 3. Authorization Runtime Registration (Scoped Boundary)
// --------------------------------------------------------------------
// Registers the core authorization runtime components introduced
// across Part 2, Part 3, and Part 4.
//
// - AuthorizationScope:
//   Request-scoped evaluation cache (TRN + decision caching).
//
// - IAuthorizationEngine / TrnAuthorizationEngine:
//   Deterministic TRN-based authorization engine.
//   Consumes ExecutionContext via accessor (no ctx passing).
//
// - IExecutionContextAccessor:
//   Scoped boundary exposing the resolved ExecutionContext
//   produced earlier in the pipeline (ContextMiddleware).
//
// - AddAuthorizedServices(...):
//   Automatically wraps services annotated with [RequireCapability]
//   using a DispatchProxy-based authorization interceptor.
// --------------------------------------------------------------------

builder.Services.AddScoped<AuthorizationScope>();
builder.Services.AddScoped<IAuthorizationEngine, TrnAuthorizationEngine>();
builder.Services.AddScoped<IExecutionContextAccessor, ExecutionContextAccessor>();

builder.Services.AddAuthorizedServices(typeof(Program).Assembly);


// --------------------------------------------------------------------
// 4. Distributed Cache Infrastructure (Redis)
// --------------------------------------------------------------------
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var cs = builder.Configuration.GetConnectionString("Redis")
             ?? throw new InvalidOperationException("Missing Redis connection string.");

    return ConnectionMultiplexer.Connect(cs);
});


// --------------------------------------------------------------------
// 5. Primary Context Store (Redis)
// --------------------------------------------------------------------
builder.Services.AddSingleton<RedisContextStore>();


// --------------------------------------------------------------------
// 6. Fallback Memory Store (Graceful degradation)
// --------------------------------------------------------------------
// Used by CompositeContextStore when Redis is unavailable.
// TTL is intentionally short.
builder.Services.AddSingleton(sp =>
{
    var mem = sp.GetRequiredService<IMemoryCache>();
    return new MemoryContextStore(mem, ttl: TimeSpan.FromSeconds(20));
});


// --------------------------------------------------------------------
// 7. Composite Store (Primary + Fallback)
// --------------------------------------------------------------------
// This matches Part 2 exactly.
// It exposes IContextStore used by ContextMiddleware.
builder.Services.AddSingleton<IContextStore>(sp =>
{
    var primary = sp.GetRequiredService<RedisContextStore>();
    var fallback = sp.GetRequiredService<MemoryContextStore>();

    return new CompositeContextStore(primary, fallback);
});


// --------------------------------------------------------------------
// 8. Runtime Options (Part 3)
// --------------------------------------------------------------------
builder.Services.Configure<ContextRuntimeOptions>(opt =>
{
    opt.SessionIdleTimeout = TimeSpan.FromMinutes(20);
    opt.AccessContextHeader = "X-Access-Context";
});


// --------------------------------------------------------------------
// 9. Build App
// --------------------------------------------------------------------
var app = builder.Build();


// --------------------------------------------------------------------
// 10. HTTP Pipeline Ordering (Critical)
// --------------------------------------------------------------------
app.UseRouting();

// 1️⃣ Authenticate first (JWT validation)
app.UseAuthentication();

// 2️⃣ Resolve and attach ExecutionContext
app.UseMiddleware<ExecutionContextMiddleware>();

// 3️⃣ Enforce namespace isolation (multiplex boundary)
app.UseMiddleware<NamespaceGuardMiddleware>();

// 4️⃣ Authorization engine will be introduced in Part 4
// app.UseAuthorization();

app.MapControllers();

app.Run();
