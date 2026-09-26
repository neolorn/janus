using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Sessions;
using Janus.Authorization.Grants;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Organizations;
using Janus.Privacy.Exports;

namespace Janus.Storage.Privacy.Exports;

/// <summary>
/// The parts of an export the identity, authentication and authorization tables hold,
/// read through the same ports the account page reads them through.
/// </summary>
/// <param name="accounts">Where the account row, its standing and its registration are.</param>
/// <param name="directory">Where the profile and the preferences are.</param>
/// <param name="identifiers">Where the identifiers and their roles are.</param>
/// <param name="authenticators">Where the enrolled credentials are.</param>
/// <param name="passwords">Where the account's password is.</param>
/// <param name="recoveryCodes">Where the single-use codes are.</param>
/// <param name="devices">Where the browsers the account knows are.</param>
/// <param name="memberships">Where the account's memberships are.</param>
/// <param name="grants">Where the roles the account holds are.</param>
/// <param name="sessions">Where the live sessions and their location records are.</param>
/// <param name="declarations">The preference keys the host declared.</param>
/// <param name="time">The clock a session's expiry is judged against.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, REG-ACCT-001, REG-PREF-001, REG-IDENT-002, AUTH-SESS-013
/// and CONV-DESIGN-003. The groups of REG-ACCT-001 are carried whole, so the export and
/// the account page answer the same question with the same facts. Every value crosses as
/// text: an export is read, not computed with, and one representation is one thing that
/// can disagree with the account page. No secret material crosses: a credential is
/// carried by property and label, as the account page carries it.
/// </remarks>
internal sealed class ExportSource(
    IAccountStore accounts,
    IAccountDirectory directory,
    IIdentifierDirectory identifiers,
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    IRecoveryCodeStore recoveryCodes,
    IDeviceStore devices,
    IMembershipStore memberships,
    IGrantStore grants,
    ISessionStore sessions,
    PreferenceDeclarations declarations,
    TimeProvider time) : IExportSource
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ExportSection>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        Account? account = await accounts.FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Password? password = await passwords.FindAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        RecoveryCodeSet? codes = await recoveryCodes.FindAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Device> browsers = await devices
            .StandingOfAsync(subject, now, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Membership> joined = await memberships
            .FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldProfile profile = await directory.ProfileAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        HeldPreferences preferences = await directory.PreferencesAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Session> live = await sessions
            .LiveOfAsync(subject, now, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            new ExportSection("account", [new ExportRecord(Standing(subject, account))]),
            new ExportSection("profile", [new ExportRecord(Filled(profile))]),
            Listed("identifiers", held.All.Select(identifier => Named(identifier, held))),
            Listed("identifier-backup", held.Backups.Select(Chosen)),
            Listed("credentials", Enrolled(enrolled, password)),
            Listed("recovery-codes", Kept(codes)),
            Listed("devices", browsers.Select(Remembered)),
            new ExportSection("preferences", [new ExportRecord(Settled(preferences))]),
            Listed("memberships", joined.Select(Joined)),
            Listed("membership-acknowledgements", joined.SelectMany(Acknowledged)),
            Listed(
                "grants",
                await ConferredAsync(subject, joined, now, cancellationToken).ConfigureAwait(false)),
            new ExportSection("assurance", [new ExportRecord(Reaches(enrolled, password))]),
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

    private static string Told(bool answer) => answer ? "true" : "false";

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
        values["verified"] = Told(identifier.IsVerified);
        values["primary"] = Told(identifier.IsPrimary);
        values["locked"] = Told(identifier.IsLocked);
        values["securityNotice"] =
            Told(held.NoticeSet.Any(reached => reached.Id == identifier.Id));

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

    // REG-ACCT-001: the credentials group, by property and label and never by secret
    // material. The password is one of them and is held apart from the enrolments, so
    // it is carried as the row the account page derives its own answers from.
    private static List<Dictionary<string, string>> Enrolled(
        IReadOnlyList<Authenticator> enrolled,
        Password? password)
    {
        Authenticator? preferred = SecondStep.Preferred(enrolled);

        var credentials = new List<Dictionary<string, string>>(enrolled.Count + 1);

        if (password is not null)
        {
            Dictionary<string, string> set = Values(capacity: 3);

            set["kind"] = nameof(Factor.Password);
            set["setAt"] = Moment(password.SetAt);
            set["changeRequired"] = Told(password.ChangeRequired);

            credentials.Add(set);
        }

        foreach (Authenticator credential in enrolled)
        {
            credentials.Add(Presented(credential, preferred));
        }

        return credentials;
    }

    private static Dictionary<string, string> Presented(
        Authenticator credential,
        Authenticator? preferred)
    {
        Dictionary<string, string> values = Values(capacity: 10);

        values["credential"] = credential.Id.Value.ToString();
        values["kind"] = credential.Factor.ToString();
        values["label"] = credential.Label.Value;
        values["state"] = credential.State.ToString();
        values["preferred"] = Told(preferred is not null && preferred.Id == credential.Id);
        values["addedAt"] = Moment(credential.AddedAt);

        if (credential.LastUsedAt is DateTimeOffset used)
        {
            values["lastUsedAt"] = Moment(used);
        }

        if (credential.InvalidatesAt is DateTimeOffset invalidates)
        {
            values["invalidatesAt"] = Moment(invalidates);
        }

        if (credential.WebAuthn is WebAuthnMaterial material)
        {
            values["backupEligible"] = Told(material.BackupEligible);
            values["backupState"] = Told(material.BackupState);
        }

        return values;
    }

    // AUTH-FACT-008 AC2 and AUTH-FACT-009 AC2: how the set stands and never a code
    // of it, which is what the account shows and all an export may carry of a secret
    // that is not retrievable after the screen that issued it.
    private static List<Dictionary<string, string>> Kept(RecoveryCodeSet? codes)
    {
        if (codes is null)
        {
            return [];
        }

        Dictionary<string, string> values = Values(capacity: 5);

        values["remaining"] = codes.Remaining.ToString(CultureInfo.InvariantCulture);
        values["generatedAt"] = Moment(codes.GeneratedAt);

        if (codes.ViewedAt is DateTimeOffset viewed)
        {
            values["viewedAt"] = Moment(viewed);
        }

        if (codes.ExportedAt is DateTimeOffset exported)
        {
            values["exportedAt"] = Moment(exported);
        }

        if (codes.RemindedAt is DateTimeOffset reminded)
        {
            values["remindedAt"] = Moment(reminded);
        }

        return [values];
    }

    // AUTH-FACT-015: the browsers the account is known at. The row holds a
    // fingerprint of the token and never the token, so what crosses is what the
    // person would recognise and nothing that would sign anyone in.
    private static Dictionary<string, string> Remembered(Device device)
    {
        Dictionary<string, string> values = Values(capacity: 6);

        values["device"] = device.Id.Value.ToString();
        values["kind"] = device.Kind.ToString();
        values["label"] = device.Label.Value;
        values["knownSince"] = Moment(device.CreatedAt);
        values["lastUsedAt"] = Moment(device.LastUsedAt);
        values["expiresAt"] = Moment(device.ExpiresAt);

        return values;
    }

    // IDN-MEM-001: a membership is a record of its own, and one that has ended is
    // still held, so the export carries it with the instant it ended on it.
    private static Dictionary<string, string> Joined(Membership membership)
    {
        Dictionary<string, string> values = Values(capacity: 4);

        values["membership"] = membership.Id.Value.ToString();
        values["organization"] = membership.Organization.Value.ToString();
        values["joinedAt"] = Moment(membership.CreatedAt);

        if (membership.EndedAt is DateTimeOffset ended)
        {
            values["endedAt"] = Moment(ended);
        }

        if (membership.Acknowledgement is MembershipAcknowledgement acknowledged)
        {
            values["acknowledgedAt"] = Moment(acknowledged.At);
        }

        return values;
    }

    // REG-INV-001 AC3: what the person acknowledged when an invitation attached the
    // membership is held on it, one record for each document at the version shown.
    private static IEnumerable<Dictionary<string, string>> Acknowledged(Membership membership)
    {
        if (membership.Acknowledgement is not MembershipAcknowledgement acknowledged)
        {
            yield break;
        }

        foreach (InvitationDocument document in acknowledged.Documents)
        {
            Dictionary<string, string> values = Values(capacity: 4);

            values["membership"] = membership.Id.Value.ToString();
            values["document"] = document.Document;
            values["version"] = document.Version;
            values["acknowledgedAt"] = Moment(acknowledged.At);

            yield return values;
        }
    }

    // AUTHZ-GRANT-001: what the account holds itself. A grant a group holds is the
    // group's record, and it reaches the person through a membership of the group.
    private static Dictionary<string, string> Conferred(Grant grant)
    {
        Dictionary<string, string> values = Values(capacity: 9);

        values["grant"] = grant.Id.Value.ToString();
        values["organization"] = grant.Organization.Value.ToString();
        values["role"] = grant.Role.ToString();
        values["deny"] = Told(grant.Deny);
        values["kind"] = grant.Kind.ToString();
        values["grantedAt"] = Moment(grant.GrantedAt);

        if (grant.ResourceType is ResourceType type)
        {
            values["resourceType"] = type.ToString();
        }

        if (grant.ResourceId is ResourceId resource)
        {
            values["resource"] = resource.ToString();
        }

        if (grant.ExpiresAt is DateTimeOffset expires)
        {
            values["expiresAt"] = Moment(expires);
        }

        return values;
    }

    // AUTH-STEP-002: the tier the account can reach with what still stands against
    // it, which is the standing REG-ACCT-001 names and not the tier of one session.
    private static Dictionary<string, string> Reaches(
        IReadOnlyList<Authenticator> enrolled,
        Password? password)
    {
        Assurance reachable = StepUp.Reachable(
            HeldFactors.Of(enrolled, password is not null).Standing);

        Dictionary<string, string> values = Values(capacity: 2);

        values["reachable"] = reachable.Level.ToString();
        values["phishingResistant"] = Told(reachable.PhishingResistant);

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

    // REG-ACCT-001: the standing group's own fields, the terms step's record among
    // them (REG-SESS-007): the notice the person was shown and the affirmation the
    // age screen derived.
    private static Dictionary<string, string> Standing(SubjectId subject, Account? account)
    {
        Dictionary<string, string> values = Values(capacity: 8);

        values["subject"] = subject.Value.ToString();

        if (account is null)
        {
            return values;
        }

        values["state"] = account.State.ToString();
        values["registeredAt"] = Moment(account.CreatedAt);

        if (account.Registration is not AccountRegistration registered)
        {
            return values;
        }

        values["termsVersion"] = registered.TermsVersion;
        values["noticeVersion"] = registered.NoticeVersion;
        values["answeredAgeAt"] = Moment(registered.AnsweredAgeAt);

        if (registered.AdultAffirmed is bool adult)
        {
            values["adultAffirmed"] = Told(adult);
        }

        if (registered.Group is AgeGroup band)
        {
            values["ageGroup"] = band.ToString();
        }

        return values;
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

    private async ValueTask<List<Dictionary<string, string>>> ConferredAsync(
        SubjectId subject,
        IReadOnlyList<Membership> joined,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        GrantSubject[] holder = [GrantSubject.Of(subject)];

        var conferred = new List<Dictionary<string, string>>();
        var asked = new HashSet<OrganizationId>();

        foreach (Membership membership in joined)
        {
            if (!asked.Add(membership.Organization))
            {
                continue;
            }

            IReadOnlyList<Grant> held = await grants
                .HeldByAsync(holder, membership.Organization, now, cancellationToken)
                .ConfigureAwait(false);

            conferred.AddRange(held.Select(Conferred));
        }

        return conferred;
    }
}
