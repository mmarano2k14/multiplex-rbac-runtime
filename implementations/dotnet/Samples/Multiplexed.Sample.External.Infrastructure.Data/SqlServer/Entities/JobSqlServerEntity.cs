using System;
using System.Collections.Generic;
using System.Text;

namespace Multiplexed.Sample.External.Infrastructure.Data.SqlServer.Entities
{
    public sealed class JobSqlServerEntity
    {
        /// <summary>
        /// Gets or sets the unique job identifier.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the job title.
        /// </summary>
        public string Title { get; set; } = default!;

        /// <summary>
        /// Gets or sets the job description.
        /// </summary>
        public string? Description { get; set; }
    }
}
