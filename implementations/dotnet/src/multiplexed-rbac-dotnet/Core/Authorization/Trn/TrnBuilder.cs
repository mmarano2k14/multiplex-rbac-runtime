using Microsoft.Extensions.Options;

namespace MultiplexedRbac.Core.Authorization.Trn
{
    public sealed class TrnBuilder
    {
        private readonly string _project;

        public TrnBuilder(IOptions<TrnBuilderOptions> options)
        {
            var project = options.Value.Project;

            if (string.IsNullOrWhiteSpace(project))
                throw new ArgumentException("Project cannot be null or empty.", nameof(project));

            _project = project.Trim().ToLowerInvariant();
        }

        public string Build(string ns, string resource, string feature, string action)
        {
            if (string.IsNullOrWhiteSpace(ns))
                throw new ArgumentException("Namespace cannot be null.", nameof(ns));
            if (string.IsNullOrWhiteSpace(resource))
                throw new ArgumentException("Resource cannot be null.", nameof(resource));
            if (string.IsNullOrWhiteSpace(feature))
                throw new ArgumentException("Feature cannot be null.", nameof(feature));
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Action cannot be null.", nameof(action));

            return $"trn:{_project}:{ns}:{resource}:{feature}:{action}";
        }
    }
}