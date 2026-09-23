using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Policies;

/// <summary>
/// The administrative organization, which is whatever a test names and nothing until
/// it names one, as before bootstrap.
/// </summary>
internal sealed class AdministrativeOrganizationInMemory : IAdministrativeOrganization
{
    /// <summary>
    /// The organization that administers the deployment.
    /// </summary>
    public OrganizationId? Organization { get; set; }

    /// <inheritdoc/>
    public ValueTask<OrganizationId?> FindAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Organization);
}
