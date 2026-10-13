using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Registration;
using Janus.Core;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// What the registration flow asks the account tables: who holds an identifier, whether
/// one is held out of reach for an undo, the language its holder settled on, and the one
/// write that creates an account.
/// </summary>
/// <param name="identifiers">
/// The account identifiers whose removals hold values out of reach, where the flow runs
/// beside them.
/// </param>
internal sealed class RegistrationDirectoryInMemory(IIdentifierDirectory? identifiers) : IRegistrationDirectory
{
    private readonly Dictionary<(IdentifierKind Kind, string Canonical), SubjectId> _owners = [];
    private readonly Dictionary<(IdentifierKind Kind, string Canonical), DateTimeOffset> _reserved = [];
    private readonly List<NewAccount> _created = [];
    private readonly Dictionary<SubjectId, string> _languages = [];

    /// <summary>
    /// A directory that holds no account identifiers beside it.
    /// </summary>
    public RegistrationDirectoryInMemory()
        : this(identifiers: null)
    {
    }

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

    /// <summary>
    /// Holds an identifier out of reach for an undo until an instant, which is what a
    /// removal whose undo window is open does.
    /// </summary>
    /// <param name="kind">Which kind of identifier.</param>
    /// <param name="canonical">Its canonical form.</param>
    /// <param name="until">When the undo window closes.</param>
    public void Reserved(IdentifierKind kind, string canonical, DateTimeOffset until) =>
        _reserved[(kind, canonical)] = until;

    /// <summary>
    /// Settles the language an account reads.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="language">The language.</param>
    public void Reads(SubjectId subject, string language) => _languages[subject] = language;

    /// <inheritdoc/>
    public ValueTask<SubjectId?> OwnerAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_owners.TryGetValue((kind, canonical), out SubjectId owner)
            ? owner
            : (SubjectId?)null);

    /// <inheritdoc/>
    public async ValueTask<bool> IsReservedAsync(
        IdentifierKind kind,
        string canonical,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (_reserved.TryGetValue((kind, canonical), out DateTimeOffset until) && until > now)
        {
            return true;
        }

        return identifiers is not null
            && await identifiers.ReservedToAsync(kind, canonical, now, cancellationToken) is not null;
    }

    /// <summary>
    /// Every value an operation locked, in the order it asked for them.
    /// </summary>
    public List<(IdentifierKind Kind, string Canonical)> Locked { get; } = [];

    /// <summary>
    /// What another transaction committed on a value while this one waited for its
    /// lock, applied as the lock is taken.
    /// </summary>
    public Func<IReadOnlyList<(IdentifierKind Kind, string Canonical)>, ValueTask>? Locking { get; set; }

    /// <inheritdoc/>
    public ValueTask LockValuesAsync(
        IReadOnlyList<(IdentifierKind Kind, string Canonical)> values,
        CancellationToken cancellationToken)
    {
        Locked.AddRange(values);

        return Locking?.Invoke(values) ?? ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<string?> LanguageAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_languages.GetValueOrDefault(subject));

    /// <inheritdoc/>
    public ValueTask CreateAsync(NewAccount account, CancellationToken cancellationToken)
    {
        _created.Add(account);

        if (account.Language is string language)
        {
            _languages[account.Subject] = language;
        }

        return ValueTask.CompletedTask;
    }
}
