namespace Multiplexed.Sample.External.Plugins.Rag.DataSources.Candidate.Resolvers
{
    public interface ICandidateRagDataSourceResolver
    {
        Multiplexed.AI.Runtime.AI.Rag.Data.AbstractRagRowDataSource Resolve(string providerKey);
    }
}