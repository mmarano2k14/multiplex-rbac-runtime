using Multiplexed.AI.Runtime.AI.Rag.Data;

namespace Multiplexed.Sample.External.Plugins.Rag.DataSources.Job.Resolvers
{
    /// <summary>
    /// Resolves the job RAG datasource based on provider key.
    /// </summary>
    public interface IJobRagDataSourceResolver
    {
        AbstractRagRowDataSource Resolve(string providerKey);
    }
}