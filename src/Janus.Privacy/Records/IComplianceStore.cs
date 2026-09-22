using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Records;

/// <summary>
/// Where the three fields of the records of processing a person supplies are kept.
/// One row for the deployment, because the register is one register.
/// </summary>
/// <remarks>Implements PRIV-ROPA-001 and CONV-DESIGN-003.</remarks>
internal interface IComplianceStore
{
    /// <summary>
    /// What the deployment last stated, empty where it has stated nothing.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The record.</returns>
    ValueTask<ComplianceRecord> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces what the deployment stated.
    /// </summary>
    /// <param name="record">What it states now.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(ComplianceRecord record, CancellationToken cancellationToken);
}
