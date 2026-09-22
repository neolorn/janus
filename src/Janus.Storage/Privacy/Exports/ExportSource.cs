using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Privacy.Exports;

namespace Janus.Storage.Privacy.Exports;

/// <summary>
/// The parts of an export the identity and authentication tables hold, read through
/// the same ports the account page reads them through.
/// </summary>
/// <param name="directory">Where the standing, the profile and the preferences are.</param>
/// <param name="identifiers">Where the identifiers and their roles are.</param>
/// <param name="sessions">Where the live sessions and their location records are.</param>
/// <param name="declarations">The preference keys the host declared.</param>
/// <param name="time">The clock a session's expiry is judged against.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, REG-PREF-001, REG-IDENT-002, AUTH-SESS-013 and
/// CONV-DESIGN-003. Every value crosses as text: an export is read, not computed
/// with, and one representation is one thing that can disagree with the account page.
/// </remarks>
internal sealed class ExportSource(
    IAccountDirectory directory,
    IIdentifierDirectory identifiers,
    ISessionStore sessions,
    PreferenceDeclarations declarations,
    TimeProvider time) : IExportSource
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ExportSection>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldProfile profile = await directory.ProfileAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldPreferences preferences = await directory.PreferencesAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Session> live = await sessions
            .LiveOfAsync(subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return
        [
            new ExportSection("account", [new ExportRecord(await StandingAsync(subject, cancellationToken).ConfigureAwait(false))]),
            new ExportSection("profile", [new ExportRecord(Filled(profile))]),
            Listed("identifiers", held.All.Select(identifier => Named(identifier, held))),
            Listed("identifier-backup", held.Backups.Select(Chosen)),
            new ExportSection("preferences", [new ExportRecord(Settled(preferences))]),
            Listed("sessions", live.Select(Used)),
        ];
    }

    private static ExportSection Listed(
        string name,
        IEnumerable<IReadOnlyDictionary<string, string>> records) =>
        new(name, [.. records.Select(values => new ExportRecord(values))]);

    private static Dictionary<string, string> Values(int capacity) =>
        new(capacity, StringComparer.Ordinal);

    private static string Moment(DateTimeOffset at) =>
        at.ToString("O", CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Filled(HeldProfile profile)
    {
        Dictionary<string, string> values = Values(capacity: 4);

        if (profile.DisplayName is DisplayName display)
        {
            values["displayName"] = display.Value;
        }

        if (profile.LegalName is LegalName legal)
        {
            values["legalName"] = legal.Value;
        }

        if (profile.DateOfBirth is DateOnly born)
        {
            values["dateOfBirth"] = born.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (profile.PhotoUpdatedAt is DateTimeOffset photo)
        {
            values["photoUpdatedAt"] = Moment(photo);
        }

        return values;
    }

    // REG-IDENT-002: the role is which kind it is, whether it is the primary of that
    // kind, and whether a security notice reaches it.
    private static Dictionary<string, string> Named(HeldIdentifier identifier, HeldIdentifiers held)
    {
        Dictionary<string, string> values = Values(capacity: 7);

        values["kind"] = identifier.Kind.ToString();
        values["value"] = identifier.Entered;
        values["verified"] = identifier.IsVerified ? "true" : "false";
        values["primary"] = identifier.IsPrimary ? "true" : "false";
        values["locked"] = identifier.IsLocked ? "true" : "false";
        values["securityNotice"] =
            held.NoticeSet.Any(reached => reached.Id == identifier.Id) ? "true" : "false";

        if (identifier.VerifiedAt is DateTimeOffset verified)
        {
            values["verifiedAt"] = Moment(verified);
        }

        return values;
    }

    private static Dictionary<string, string> Chosen(HeldBackup backup)
    {
        Dictionary<string, string> values = Values(capacity: 3);

        values["kind"] = backup.Kind.ToString();
        values["setting"] = backup.Choice.ToString();

        if (backup.Named is IdentifierId named)
        {
            values["named"] = named.Value.ToString();
        }

        return values;
    }

    // AUTH-SESS-013: the location is resolved at sign-in and at last use, so the
    // export carries both and not one standing for the other.
    private static Dictionary<string, string> Used(Session session)
    {
        Dictionary<string, string> values = Values(capacity: 9);

        values["session"] = session.Id.Value.ToString();
        values["signedInAt"] = Moment(session.CreatedAt);
        values["lastUsedAt"] = Moment(session.LastSeenAt);
        values["browser"] = session.LastSeen.Device.Browser;
        values["operatingSystem"] = session.LastSeen.Device.Os;

        Placed(values, "signedIn", session.Origin.Location);
        Placed(values, "lastUsed", session.LastSeen.Location);

        return values;
    }

    private static void Placed(
        Dictionary<string, string> values,
        string occasion,
        SessionLocation? location)
    {
        if (location?.City is string city)
        {
            values[occasion + "City"] = city;
        }

        if (location?.Country is string country)
        {
            values[occasion + "Country"] = country;
        }
    }

    private Dictionary<string, string> Settled(HeldPreferences preferences)
    {
        Dictionary<string, string> values = Values(capacity: 2 + declarations.All.Count);

        if (preferences.Language is string language)
        {
            values["language"] = language;
        }

        if (preferences.TimeZone is string zone)
        {
            values["timeZone"] = zone;
        }

        // REG-PREF-001: the export carries the value in force, which for a key the
        // account never set is the default the host declared.
        foreach (PreferenceDeclaration declaration in declarations.All)
        {
            values[declaration.Name] =
                preferences.Values.TryGetValue(declaration.Name, out string? set)
                    ? set
                    : declaration.Default;
        }

        return values;
    }

    private async ValueTask<Dictionary<string, string>> StandingAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> values = Values(capacity: 3);

        values["subject"] = subject.Value.ToString();

        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is AccountState state)
        {
            values["state"] = state.ToString();
        }

        if (await directory.CreatedAtAsync(subject, cancellationToken).ConfigureAwait(false)
            is DateTimeOffset created)
        {
            values["registeredAt"] = Moment(created);
        }

        return values;
    }
}
