using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;

namespace Janus.Authorization.Tests.Grants;

/// <summary>
/// The reserved emergency account, which is whatever a test names and nothing until it
/// names one, as before bootstrap.
/// </summary>
internal sealed class EmergencyAccountInMemory : IEmergencyAccount
{
    /// <summary>
    /// The account the break-glass session belongs to.
    /// </summary>
    public SubjectId? Account { get; set; }

    /// <inheritdoc/>
    public ValueTask<SubjectId?> FindAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Account);
}
