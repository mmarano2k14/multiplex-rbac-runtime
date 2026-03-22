using MultiplexedRbac.Core.Authorization.Attributes;
using MultiplexedRbac.Core.Authorization.Engine;
using System.Reflection;

namespace MultiplexedRbac.Core.Authorization.Proxy
{
    internal class  AuthorizationProxy<T> : DispatchProxy where T : class
    {
        public T Inner { get; set; } = default!;
        public IAuthorizationEngine Auth { get; set; } = default!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new ArgumentNullException(nameof(targetMethod));

            // Interface type-level attributes
            var interfaceTypeAttributes = typeof(T)
                .GetCustomAttributes<RequireCapabilityAttribute>(true);

            // Interface method-level attributes
            var interfaceMethodAttributes = targetMethod
                .GetCustomAttributes<RequireCapabilityAttribute>(true);

            // Implementation type
            var implType = Inner.GetType();

            // Class-level attributes (implementation)
            var implTypeAttributes = implType
                .GetCustomAttributes<RequireCapabilityAttribute>(true);

            // Implementation method-level attributes
            var implMethod = ResolveImplementationMethod(implType, targetMethod);

            var implMethodAttributes = implMethod is null
                ? Enumerable.Empty<RequireCapabilityAttribute>()
                : implMethod.GetCustomAttributes<RequireCapabilityAttribute>(true);

            // Combine ALL (AND)
            var allRequirements = interfaceTypeAttributes
                .Concat(interfaceMethodAttributes)
                .Concat(implTypeAttributes)
                .Concat(implMethodAttributes);

            foreach (var req in allRequirements)
            {
                if (!Auth.IsAllowed(req.Resource, req.Feature, req.Action))
                {
                    throw new UnauthorizedAccessException(
                        $"Missing capability: {req.Resource}:{req.Feature}:{req.Action}");
                }
            }

            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        private static MethodInfo? ResolveImplementationMethod(
            Type implType,
            MethodInfo interfaceMethod)
        {
            var paramTypes = interfaceMethod
                .GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();

            return implType.GetMethod(interfaceMethod.Name, paramTypes);
        }
    }
}
