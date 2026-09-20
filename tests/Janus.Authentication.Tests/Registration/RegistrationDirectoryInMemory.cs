using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// What the registration flow asks the account tables: who holds an identifier, and
/// the one write that creates an account.
/// </summary>
internal sealed class RegistrationDirectoryInMemory : IRegistrationDirectory
{
    private readonly Dictionary<(IdentifierKind Kind, string Canonical), SubjectId> _owners = [];
    private readonly List<NewAccount> _created = [];

    /// <summary>
    /// Every account the flow created, in the order it created them.
    /// </summary>
    public IReadOnlyList<NewAccount> Created => _created;

    /// <summary>
    /// Gives an identifier an owner, which is what makes a registration with it a
    /// duplicate.
    /// </summary>
    /// <param name="kind">Which kind of identifier.</param>
    /// <param name="canonical">Its canonical form.</param>
    /// <param name="owner">Who holds it.</param>
    public void Held(IdentifierKind kind, string canonical, SubjectId owner) =>
        _owners[(kind, canonical)] = owner;

    /// <inheritdoc/>
    public ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_owners.TryGetValue((kind, canonical), out SubjectId owner)
            ? owner
            : (SubjectId?)null);

    /// <inheritdoc/>
    public ValueTask CreateAsync(NewAccount account, CancellationToken cancellationToken)
    {
        _created.Add(account);

        return ValueTask.CompletedTask;
    }
}
