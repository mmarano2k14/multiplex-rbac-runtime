using MultiplexedRbac.Core.Authorization.Attributes;
using MultiplexedRbac.Core.Authorization.Engine;
using MultiplexedRbac.Core.Authorization.Proxy;
using System.Reflection;

namespace MultiplexedRbac.Core.Authorization.Registration
{
    public static class AuthorizationRegistration
    {
        public static IServiceCollection AddAuthorizedServices(
            this IServiceCollection services,
            Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract);

            foreach (var impl in types)
            {
                var interfaces = impl.GetInterfaces()
                    .Where(IsAuthorizationCandidate)
                    .ToArray();

                if (interfaces.Length == 0)
                    continue;

                services.AddScoped(impl);

                foreach (var itf in interfaces)
                {
                    services.AddScoped(itf, sp =>
                    {
                        var inner = sp.GetRequiredService(impl);
                        var auth = sp.GetRequiredService<IAuthorizationEngine>();
                        return AuthorizationProxyFactory.Create(inner, auth);
                    });
                }
            }

            return services;
        }

        private static bool IsAuthorizationCandidate(Type type)
        {
            if (type.GetCustomAttributes<RequireCapabilityAttribute>(true).Any())
                return true;

            return type.GetMethods()
                .Any(m => m.GetCustomAttributes<RequireCapabilityAttribute>(true).Any());
        }
    }
}
