using MultiplexedRbac.Core.Authorization.Engine;
using System.Reflection;

namespace MultiplexedRbac.Core.Authorization.Proxy
{
    public static class AuthorizationProxyFactory
    {
        public static T Create<T>(T inner, IAuthorizationEngine auth) where T : class
        {
            var proxy = DispatchProxy.Create<T, AuthorizationProxy<T>>();

            var typed = (AuthorizationProxy<T>)(object)proxy!;
            typed.Inner = inner;
            typed.Auth = auth;

            return proxy!;
        }
    }
}
