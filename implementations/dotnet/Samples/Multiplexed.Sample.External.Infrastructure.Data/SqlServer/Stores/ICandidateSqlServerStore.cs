using Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Stores
{
    /// <summary>
    /// Provides SQL Server-shaped candidate data for the external infrastructure layer.
    ///
    /// PURPOSE:
    /// - Abstracts SQL Server candidate data access.
    /// - Allows switching between EF implementation and in-memory implementation.
    ///
    /// DESIGN:
    /// - Provider-specific contract (SQL Server only).
    /// - Used by higher-level query components.
    ///
    /// IMPORTANT:
    /// - Must return deterministic results for the same input.
    /// </summary>
    public interface ICandidateSqlServerStore
    {
        /// <summary>
        /// Reads candidate records by identifier.
        /// </summary>
        Task<IReadOnlyList<CandidateSqlServerEntity>> ReadByIdAsync(
            string candidateId,
            CancellationToken cancellationToken = default);
    }
}