using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Factors;

/// <summary>
/// Creating a WebAuthn credential and presenting one: the passkey that signs a person
/// in and the security key that stands beside a password.
/// </summary>
/// <param name="authenticators">Where enrolled credentials are read and written.</param>
/// <param name="passwords">Where the account's password is read.</param>
/// <param name="audit">Where an event about a credential is recorded.</param>
/// <param name="configuration">Where the relying party is read from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a challenge is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-011, AUTH-FACT-013 and AUTH-FACT-014, and the two kinds of
/// AUTH-FACT-002b. The relying party is settled from configuration before a ceremony
/// begins, so a deployment whose configuration does not hold together enrols nothing
/// (AUTH-FACT-010).
/// </remarks>
internal sealed class WebAuthnService(
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    ICredentialAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    private static readonly AuditAction CounterMoved =
        AuditAction.Parse("auth.credential.countermismatch");

    /// <summary>
    /// Begins a creation ceremony of the kind asked for.
    /// </summary>
    /// <param name="kind">A passkey or a second-factor security key.</param>
    /// <param name="user">Who the credential is created for.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the browser is asked for, or the failure where the kind is not one a
    /// ceremony creates.
    /// </returns>
    /// <exception cref="ArgumentNullException">The user is absent.</exception>
    public async ValueTask<Result<WebAuthnCeremony>> BeginAsync(
        Factor kind,
        CeremonyUser user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        FactorProperties entry = FactorCatalogue.Of(kind);

        if (!entry.IsWebAuthn)
        {
            return Result.Failure<WebAuthnCeremony>(Error.From(ErrorCodes.FactorRejected));
        }

        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new WebAuthnCeremony(
            party.Id,
            user,
            party.Algorithms,
            entry.IsDiscoverable,
            OpaqueToken.Draw(randomness).Value));
    }

    /// <summary>
    /// The handle an account's credentials are created under (REG-PM-001).
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <returns>The subject identifier as a WebAuthn user handle.</returns>
    public static string Handle(SubjectId subject) =>
        Base64Url.EncodeToString(subject.Value.ToByteArray(bigEndian: true));

    /// <summary>
    /// Records the credential a completed ceremony produced.
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <param name="kind">A passkey or a second-factor security key.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="registration">What the ceremony produced.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential's identifier, or the failure where the ceremony reached none of
    /// what the chapter requires of it.
    /// </returns>
    /// <exception cref="ArgumentNullException">The registration is absent.</exception>
    public async ValueTask<Result<AuthenticatorId>> CompleteAsync(
        SubjectId subject,
        Factor kind,
        CredentialLabel label,
        WebAuthnRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (SecondStep.Is(kind)
            && !await SecondStep.AvailableAsync(passwords, subject, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<AuthenticatorId>(Error.From(ErrorCodes.FactorNotPermitted));
        }

        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        Error? refusal = Admits(party, kind, registration);

        if (refusal is not null)
        {
            return Result.Failure<AuthenticatorId>(refusal);
        }

        Authenticator enrolled = Enrolled(subject, kind, label, registration, party);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await authenticators.AddAsync(enrolled, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(enrolled.Id);
    }

    /// <summary>
    /// Replaces a second-factor security key with a passkey held on the same hardware,
    /// retiring the entry it came from.
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <param name="id">The second-factor entry being upgraded.</param>
    /// <param name="label">What the person calls the passkey.</param>
    /// <param name="registration">What the passkey ceremony produced.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The passkey's identifier, or the failure where the entry is not theirs, is not
    /// a security key, or the ceremony is refused. Nothing changes on a failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">The registration is absent.</exception>
    public async ValueTask<Result<AuthenticatorId>> UpgradeAsync(
        SubjectId subject,
        AuthenticatorId id,
        CredentialLabel label,
        WebAuthnRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        Authenticator? upgrading = await authenticators.FindAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (upgrading is null || upgrading.Subject != subject)
        {
            return Result.Failure<AuthenticatorId>(Error.From(ErrorCodes.CredentialNotFound));
        }

        if (!IsSecondFactorKey(upgrading))
        {
            return Result.Failure<AuthenticatorId>(
                Error.From(ErrorCodes.CredentialNotUpgradable));
        }

        Factor discoverable = FactorCatalogue.Discoverable;

        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        Error? refusal = Admits(party, discoverable, registration);

        if (refusal is not null)
        {
            return Result.Failure<AuthenticatorId>(refusal);
        }

        Authenticator enrolled = Enrolled(subject, discoverable, label, registration, party);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        upgrading.Invalidate();
        await authenticators.AddAsync(enrolled, cancellationToken).ConfigureAwait(false);
        await authenticators.RecordAsync(upgrading, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(enrolled.Id);
    }

    /// <summary>
    /// Enrols what an enrolment ceremony answered, reading and checking the ceremony
    /// before anything about it is believed.
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <param name="kind">Which kind is being created.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="answered">What the browser sent back.</param>
    /// <param name="challenge">The value the ceremony was opened with.</param>
    /// <param name="upgrading">
    /// The second-factor entry being replaced, where this is an upgrade, and nothing
    /// otherwise.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credential's identifier, or the refusal.</returns>
    public async ValueTask<Result<AuthenticatorId>> EnrolAsync(
        SubjectId subject,
        Factor kind,
        CredentialLabel label,
        AuthenticatorAttestation answered,
        string challenge,
        AuthenticatorId? upgrading,
        CancellationToken cancellationToken)
    {
        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        Error? refusal = null;

        WebAuthnRegistration registration = WebAuthnCeremonies
            .Created(answered, challenge, party.Origins, party.Id)
            .Match(read => read, error => Withheld<WebAuthnRegistration>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<AuthenticatorId>(refusal);
        }

        return upgrading is AuthenticatorId retiring
            ? await UpgradeAsync(subject, retiring, label, registration, cancellationToken)
                .ConfigureAwait(false)
            : await CompleteAsync(subject, kind, label, registration, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Judges what a sign-in ceremony answered against the credential it names,
    /// reading and checking the ceremony first.
    /// </summary>
    /// <param name="answered">What the browser sent back.</param>
    /// <param name="challenge">The value the sign-in was opened with.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credential that answered, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The answer is absent.</exception>
    public async ValueTask<Result<Authenticator>> AssertAsync(
        AuthenticatorAssertion answered,
        string challenge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(answered);

        byte[]? credentialId = Read(answered.CredentialId);

        if (credentialId is null)
        {
            return Result.Failure<Authenticator>(Error.From(ErrorCodes.FactorRejected));
        }

        Authenticator? held = await authenticators
            .ByCredentialAsync(credentialId, cancellationToken)
            .ConfigureAwait(false);

        if (held?.WebAuthn is null)
        {
            return Result.Failure<Authenticator>(Error.From(ErrorCodes.FactorRejected));
        }

        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        Error? refusal = null;

        WebAuthnAssertion assertion = WebAuthnCeremonies
            .Asserted(answered, challenge, party.Origins, held.WebAuthn)
            .Match(read => read, error => Withheld<WebAuthnAssertion>(error, ref refusal));

        return refusal is not null
            ? Result.Failure<Authenticator>(refusal)
            : await PresentAsync(assertion, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Judges an assertion against the credential it names.
    /// </summary>
    /// <param name="assertion">What the ceremony produced.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential that answered, or the failure where it is unusable, was enrolled
    /// under another relying party, verified nobody, or reported a counter that moved
    /// backwards.
    /// </returns>
    /// <exception cref="ArgumentNullException">The assertion is absent.</exception>
    public async ValueTask<Result<Authenticator>> PresentAsync(
        WebAuthnAssertion assertion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        Authenticator? held = await authenticators
            .ByCredentialAsync(assertion.CredentialId, cancellationToken)
            .ConfigureAwait(false);

        if (held is null || !held.IsUsable || held.WebAuthn is null)
        {
            return Result.Failure<Authenticator>(Error.From(ErrorCodes.FactorRejected));
        }

        // REG-PM-001: the handle a discoverable credential returns is the account it
        // was created for, so it is what says whose credential answered. A handle
        // naming another account, or none the library ever issued, is refused exactly
        // as a wrong credential is: whose it is is not disclosed.
        if (assertion.UserHandle is { Length: > 0 } returned && Named(returned) != held.Subject)
        {
            return Result.Failure<Authenticator>(Error.From(ErrorCodes.FactorRejected));
        }

        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        if (!party.Binds(held.WebAuthn.RelyingPartyId) || !party.Binds(assertion.RelyingPartyId))
        {
            return Result.Failure<Authenticator>(Error.From(ErrorCodes.WebAuthnRelyingPartyChanged));
        }

        if (!assertion.UserVerified)
        {
            return Result.Failure<Authenticator>(
                Error.From(ErrorCodes.WebAuthnUserVerificationRequired));
        }

        DateTimeOffset now = time.GetUtcNow();

        // A counter that did not advance is a credential that exists twice. An
        // authenticator that keeps no counter reports nought every time, which is the
        // absence the chapter excludes and not a counter standing still.
        if (Kept(assertion.Counter) && Kept(held.WebAuthn.Counter) && assertion.Counter <= held.WebAuthn.Counter)
        {
            await audit.RecordedAsync(CounterMoved, held.Subject, held.Id, now, cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure<Authenticator>(Error.From(ErrorCodes.WebAuthnCounterMismatch));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (Kept(assertion.Counter))
        {
            held.Counted(assertion.Counter);
        }

        held.Used(now);
        await authenticators.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(held);
    }

    /// <summary>
    /// The account's credentials that were enrolled under a relying party identifier
    /// no longer in force, which is what asks their owner to enrol again.
    /// </summary>
    /// <param name="subject">Whose credentials.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credentials standing under a previous identifier.</returns>
    public async ValueTask<IReadOnlyList<Authenticator>> StaleAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Authenticator> held = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return [.. held.Where(credential =>
            credential.WebAuthn is not null && !party.Binds(credential.WebAuthn.RelyingPartyId))];
    }

    private static bool Kept(uint? counter) => counter is > 0;

    private static SubjectId? Named(string handle) =>
        Read(handle) is { Length: 16 } bytes
            ? new SubjectId(new Guid(bytes, bigEndian: true))
            : null;

    private static byte[]? Read(string value) =>
        value is not null && Base64Url.IsValid(value) ? Base64Url.DecodeFromChars(value) : null;

    private static TValue Withheld<TValue>(Error error, ref Error? refusal)
    {
        refusal = error;

        return default!;
    }

    // The credential an upgrade moves from: a WebAuthn credential the ceremony did
    // not make discoverable, which is what a second step holds (AUTH-FACT-002b).
    private static bool IsSecondFactorKey(Authenticator credential) =>
        FactorCatalogue.Of(credential.Factor) is { IsWebAuthn: true, IsDiscoverable: false };

    // Attestation is not required of any of this: what the ceremony has to reach is
    // an algorithm the deployment admits, the relying party in force, and a person
    // the authenticator verified (AUTH-FACT-014).
    private static Error? Admits(RelyingParty party, Factor kind, WebAuthnRegistration registration)
    {
        if (!FactorCatalogue.Of(kind).IsWebAuthn)
        {
            return Error.From(ErrorCodes.FactorRejected);
        }

        if (!party.Binds(registration.RelyingPartyId))
        {
            return Error.From(ErrorCodes.WebAuthnRelyingPartyChanged);
        }

        if (!party.Algorithms.Contains(registration.Algorithm))
        {
            return Error.From(ErrorCodes.WebAuthnAlgorithmNotAllowed);
        }

        return registration.UserVerified
            ? null
            : Error.From(ErrorCodes.WebAuthnUserVerificationRequired);
    }

    private Authenticator Enrolled(
        SubjectId subject,
        Factor kind,
        CredentialLabel label,
        WebAuthnRegistration registration,
        RelyingParty party) =>
        Authenticator.WebAuthnCredential(
            AuthenticatorId.New(time),
            subject,
            kind,
            label,
            new WebAuthnMaterial(
                registration.CredentialId,
                registration.PublicKey,
                registration.Algorithm,
                party.Id,
                registration.Counter is 0 ? null : registration.Counter,
                registration.BackupEligible,
                registration.BackupState),
            time.GetUtcNow());
}
