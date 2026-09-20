using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Preferences;
using Janus.Identity.Profiles;

namespace Janus.Storage.Authentication.Accounts;

/// <summary>
/// What the account operations ask of the account directory, over the identity
/// stores.
/// </summary>
/// <param name="accounts">Where the account row is read.</param>
/// <param name="profiles">Where the profile is read and written.</param>
/// <param name="photos">Where the photo's instant is read.</param>
/// <param name="preferences">Where the preferences are read and written.</param>
/// <param name="declarations">The preference keys the host declared.</param>
/// <remarks>
/// Implements REG-ACCT-001, REG-PROF-001, REG-PREF-001 and CONV-LAYOUT-001. Every
/// write runs inside the caller's transaction, so the whole of one operation commits
/// or none of it does.
/// </remarks>
internal sealed class AccountDirectory(
    IAccountStore accounts,
    IProfileStore profiles,
    IProfilePhotoStore photos,
    IPreferenceStore preferences,
    PreferenceDeclarations declarations) : IAccountDirectory
{
    /// <inheritdoc/>
    public async ValueTask<AccountState?> StateAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        (await accounts.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false))?.State;

    /// <inheritdoc/>
    public async ValueTask<HeldProfile> ProfileAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Profile profile = await profiles.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        ProfilePhoto? photo = await photos.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return new HeldProfile(
            profile.DisplayName,
            profile.LegalName,
            profile.DateOfBirth,
            photo?.UpdatedAt);
    }

    /// <inheritdoc/>
    public async ValueTask RecordProfileAsync(
        SubjectId subject,
        DisplayName? displayName,
        LegalName? legalName,
        CancellationToken cancellationToken)
    {
        Profile profile = await profiles.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        profile.SetDisplayName(displayName);
        profile.SetLegalName(legalName);

        await profiles.RecordAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<HeldPreferences> PreferencesAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        PreferenceSet set = await preferences.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return new HeldPreferences(set.Language, set.TimeZone, set.Values);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RecordPreferencesAsync(
        SubjectId subject,
        string? language,
        string? timeZone,
        IReadOnlyDictionary<string, string> declared,
        bool asAdministrator,
        int maximumSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);

        PreferenceSet set = await preferences.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        foreach (string name in Given(set, declared, asAdministrator))
        {
            set.Clear(name);
        }

        Error? failure = null;

        foreach (KeyValuePair<string, string> value in declared)
        {
            set.Set(name: value.Key, value: value.Value, declarations: declarations,
                    asAdministrator: asAdministrator, maximumSize: maximumSize)
                .Switch(() => { }, error => failure ??= error);
        }

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        set.SetLanguage(language);
        set.SetTimeZone(timeZone);

        await preferences.RecordAsync(set, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // A replace gives up what it left out, except a key only an administrator may
    // edit: a person who never saw it as theirs does not lose it by sending the rest
    // back (REG-PREF-001).
    private List<string> Given(
        PreferenceSet set,
        IReadOnlyDictionary<string, string> declared,
        bool asAdministrator)
    {
        var given = new List<string>();

        foreach (string name in set.Values.Keys)
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
}
