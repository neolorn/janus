using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Records;

namespace Janus.Privacy.Tests.Records;

/// <summary>
/// The roles of the deployment and what each allows, as a test arranges them.
/// </summary>
internal sealed class RegisterRolesInMemory : IRegisterRoles
{
    private readonly Dictionary<string, IReadOnlyList<Permission>> _roles =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gives a role its permissions.
    /// </summary>
    /// <param name="role">What it is called.</param>
    /// <param name="permissions">What it allows.</param>
    public void Allows(string role, params string[] permissions) =>
        _roles[role] = [.. permissions.Select(Permission.Parse)];

    /// <inheritdoc/>
    public ValueTask<IReadOnlyDictionary<string, IReadOnlyList<Permission>>> AllowedAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyDictionary<string, IReadOnlyList<Permission>>>(_roles);
}
