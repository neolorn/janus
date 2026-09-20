using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// The standing, the profile and the preferences of an account, held in memory. The
/// preference rules are the declaration's, applied here as the store applies them.
/// </summary>
/// <param name="declarations">The preference keys the host declared.</param>
internal sealed class AccountDirectoryInMemory(PreferenceDeclarations declarations) : IAccountDirectory
{
    private readonly Dictionary<SubjectId, AccountState> _states = [];
    private readonly Dictionary<SubjectId, HeldProfile> _profiles = [];
    private readonly Dictionary<SubjectId, HeldPreferences> _preferences = [];
    private readonly Dictionary<SubjectId, DateTimeOffset> _created = [];

    /// <summary>
    /// Puts an account in a state, which is what makes it exist here at all.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="state">Where it stands.</param>
    public void Stands(SubjectId subject, AccountState state) => _states[subject] = state;

    /// <summary>
    /// Says when an account came into being.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="at">When it was registered.</param>
    public void Registered(SubjectId subject, DateTimeOffset at) => _created[subject] = at;

    /// <summary>
    /// Puts a profile on an account as support would have left it.
    /// </summary>
    /// <param name="subject">Whose profile.</param>
    /// <param name="profile">What it holds.</param>
    public void Holds(SubjectId subject, HeldProfile profile) => _profiles[subject] = profile;

    /// <inheritdoc/>
    public ValueTask<AccountState?> StateAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_states.TryGetValue(subject, out AccountState state)
            ? state
            : (AccountState?)null);

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> CreatedAtAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_created.TryGetValue(subject, out DateTimeOffset at)
            ? at
            : (DateTimeOffset?)null);

    /// <inheritdoc/>
    public ValueTask<HeldProfile> ProfileAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Profile(subject));

    /// <inheritdoc/>
    public ValueTask RecordProfileAsync(
        SubjectId subject,
        DisplayName? displayName,
        LegalName? legalName,
        CancellationToken cancellationToken)
    {
        HeldProfile held = Profile(subject);

        _profiles[subject] = held with { DisplayName = displayName, LegalName = legalName };

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<HeldPreferences> PreferencesAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Preferences(subject));

    /// <inheritdoc/>
    public ValueTask<Result> RecordPreferencesAsync(
        SubjectId subject,
        string? language,
        string? timeZone,
        IReadOnlyDictionary<string, string> declared,
        bool asAdministrator,
        int maximumSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);

        HeldPreferences held = Preferences(subject);
        var values = new Dictionary<string, string>(held.Values, StringComparer.Ordinal);

        foreach (string name in Given(values, declared, asAdministrator))
        {
            _ = values.Remove(name);
        }

        foreach (KeyValuePair<string, string> value in declared)
        {
            if (Set(values, value.Key, value.Value, asAdministrator, maximumSize) is Error refused)
            {
                return ValueTask.FromResult(Result.Failure(refused));
            }
        }

        _preferences[subject] = new HeldPreferences(language, timeZone, values);

        return ValueTask.FromResult(Result.Success());
    }

    private static int Size(Dictionary<string, string> values)
    {
        int size = 0;

        foreach (KeyValuePair<string, string> value in values)
        {
            size += Encoding.UTF8.GetByteCount(value.Key) + Encoding.UTF8.GetByteCount(value.Value);
        }

        return size;
    }

    private Error? Set(
        Dictionary<string, string> values,
        string name,
        string value,
        bool asAdministrator,
        int maximumSize)
    {
        if (!declarations.TryFind(name, out PreferenceDeclaration declaration))
        {
            return Error.From(ErrorCodes.PreferenceUndeclared);
        }

        if (declaration.AdministratorOnly && !asAdministrator)
        {
            return Error.From(ErrorCodes.PreferenceAdministratorOnly);
        }

        if (!declaration.Admits(value))
        {
            return Error.From(ErrorCodes.PreferenceWrongType);
        }

        values[name] = value;

        return Size(values) > maximumSize ? Error.From(ErrorCodes.PreferenceTooLarge) : null;
    }

    // A replace gives up what it left out, except a key only an administrator may edit.
    private List<string> Given(
        Dictionary<string, string> values,
        IReadOnlyDictionary<string, string> declared,
        bool asAdministrator)
    {
        var given = new List<string>();

        foreach (string name in values.Keys)
        {
            if (declared.ContainsKey(name))
            {
                continue;
            }

            if (!asAdministrator
                && declarations.TryFind(name, out PreferenceDeclaration declaration)
                && declaration.AdministratorOnly)
            {
                continue;
            }

            given.Add(name);
        }

        return given;
    }

    private HeldProfile Profile(SubjectId subject) =>
        _profiles.TryGetValue(subject, out HeldProfile? held)
            ? held
            : new HeldProfile(null, null, null, null);

    private HeldPreferences Preferences(SubjectId subject) =>
        _preferences.TryGetValue(subject, out HeldPreferences? held)
            ? held
            : new HeldPreferences(null, null, new Dictionary<string, string>(StringComparer.Ordinal));
}
