using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Factors;

/// <summary>
/// The browsers an account knows: the one whose trust spares it a second factor, and
/// the one the new-device check has already seen.
/// </summary>
/// <param name="devices">Where the browsers are read and written.</param>
/// <param name="configuration">Where the lifetimes and the failure limit come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the emitted events go.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a token is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-015 and AUTH-FACT-016. Neither token is a credential: one
/// stands in for the second step of a sign-in the password has already begun, the
/// other for a check the person has already passed, and the session that follows
/// records only what was presented (D-141).
/// </remarks>
internal sealed class DeviceService(
    IDeviceStore devices,
    IConfigurationStore configuration,
    IUnitOfWork work,
    IEvents events,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    private const string Verified = "device-verified";

    /// <summary>
    /// Whether the account may be offered the trust of this browser, which a policy
    /// that requires two factors and a password that would complete a sign-in alone
    /// each withhold.
    /// </summary>
    /// <param name="policy">The principal's policy.</param>
    /// <param name="reached">What the sign-in reached.</param>
    /// <param name="passwordMeetsSingleFactorFloor">
    /// Whether the account's password is long enough to stand alone.
    /// </param>
    /// <returns>Whether the offer is made.</returns>
    /// <exception cref="ArgumentNullException">The policy is absent.</exception>
    public static bool MayTrust(
        Policy policy,
        Assurance reached,
        bool passwordMeetsSingleFactorFloor)
    {
        ArgumentNullException.ThrowIfNull(policy);

        // Their members hold what needs no second factor to skip, and the floor would
        // in any case refuse the shortcut (AUTH-FACT-015, AUTH-SESS-005b). On a
        // trusted device a password below the single-factor floor would complete a
        // sign-in alone, which is the one thing that floor exists to prevent
        // (AUTH-PASS-001a).
        return policy.RequiredAssurance < AssuranceLevel.Aal2
            && passwordMeetsSingleFactorFloor
            && reached.Level >= AssuranceLevel.Aal2;
    }

    /// <summary>
    /// Marks this browser as trusted, so that the second factor is skipped on it
    /// until the trust lapses.
    /// </summary>
    /// <param name="subject">Whose browser.</param>
    /// <param name="device">What the browser said it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The token the browser carries, returned once.</returns>
    public async ValueTask<Result<OpaqueToken>> TrustAsync(
        SubjectId subject,
        DeviceDescription device,
        CancellationToken cancellationToken) =>
        (await KnownAsync(
                subject,
                DeviceKind.Trusted,
                device,
                Settings.FactorTrustedDeviceLifetime,
                cancellationToken)
            .ConfigureAwait(false))
        .Match(known => Result.Success(known.Token), Result.Failure<OpaqueToken>);

    /// <summary>
    /// Records that this browser passed the new-device check, or completed the step
    /// of registration that stands for it, so that it is not held again while it is
    /// remembered.
    /// </summary>
    /// <param name="subject">Whose browser.</param>
    /// <param name="device">What the browser said it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The token the browser carries, returned once.</returns>
    public async ValueTask<Result<OpaqueToken>> RememberAsync(
        SubjectId subject,
        DeviceDescription device,
        CancellationToken cancellationToken) =>
        (await RecordedAsync(subject, device, cancellationToken).ConfigureAwait(false))
        .Match(known => Result.Success(known.Token), Result.Failure<OpaqueToken>);

    /// <summary>
    /// The new-device check passed: the browser is remembered for the period the
    /// deployment configured, and the completion is announced once.
    /// </summary>
    /// <param name="subject">Whose browser.</param>
    /// <param name="device">What the browser said it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The token the browser carries, returned once.</returns>
    public async ValueTask<Result<OpaqueToken>> VerifiedAsync(
        SubjectId subject,
        DeviceDescription device,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        (DeviceId browser, OpaqueToken token) = (await RecordedAsync(subject, device, cancellationToken)
                .ConfigureAwait(false))
            .Match(known => known, error => Held<(DeviceId Browser, OpaqueToken Token)>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<OpaqueToken>(failure);
        }

        // One browser is remembered per check, so its identifier is what a consumer
        // recognises the repeat of one check by (AUTH-FACT-016).
        Result published = await events
            .PublishAsync(
                new DeviceVerified(time.GetUtcNow(), Verified + ":" + browser, browser),
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure<OpaqueToken>(unpublished);
        }

        return Result.Success(token);
    }

    /// <summary>
    /// Whether the trust of the browser carrying this token stands for this account,
    /// which spares it the second factor of this sign-in and nothing else.
    /// </summary>
    /// <param name="subject">Whose sign-in.</param>
    /// <param name="presented">The token the browser carried, absent where it carried none.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the second step is skipped.</returns>
    public ValueTask<bool> TrustsAsync(
        SubjectId subject,
        [NeverLogged] string? presented,
        CancellationToken cancellationToken) =>
        StandsAsync(subject, presented, DeviceKind.Trusted, cancellationToken);

    /// <summary>
    /// Whether the account has seen the browser carrying this token.
    /// </summary>
    /// <param name="subject">Whose sign-in.</param>
    /// <param name="presented">The token the browser carried, absent where it carried none.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the browser is remembered.</returns>
    public ValueTask<bool> RemembersAsync(
        SubjectId subject,
        [NeverLogged] string? presented,
        CancellationToken cancellationToken) =>
        StandsAsync(subject, presented, DeviceKind.Remembered, cancellationToken);

    /// <summary>
    /// Whether this sign-in is held until a code sent to the account's primary email
    /// is entered.
    /// </summary>
    /// <param name="subject">Whose sign-in.</param>
    /// <param name="reachable">What the account can reach with what it holds.</param>
    /// <param name="presented">What the sign-in has reached so far.</param>
    /// <param name="browser">The token the browser carried, absent where it carried none.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the check holds the sign-in.</returns>
    public async ValueTask<Result<bool>> ChecksAsync(
        SubjectId subject,
        Assurance reachable,
        Assurance presented,
        string? browser,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        bool enabled = (await configuration
                .ReadAsync(Settings.DeviceVerificationEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        // An account that can reach two factors protects itself; a sign-in that
        // reached them has already shown more than a code to a mailbox would
        // (AUTH-FACT-016).
        if (!enabled
            || reachable.Level is not AssuranceLevel.Aal1
            || presented.Level >= AssuranceLevel.Aal2)
        {
            return Result.Success(false);
        }

        return Result.Success(
            !await RemembersAsync(subject, browser, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// A sign-in on this browser failed. Enough failures in a row revoke its trust,
    /// the next sign-in on it asking for the second factor again.
    /// </summary>
    /// <param name="subject">Whose sign-in.</param>
    /// <param name="presented">The token the browser carried, absent where it carried none.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure where the limit cannot be read.</returns>
    public async ValueTask<Result> FailedAsync(
        SubjectId subject,
        [NeverLogged] string? presented,
        CancellationToken cancellationToken)
    {
        Device? device = await OfAsync(subject, presented, DeviceKind.Trusted, cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
        {
            return Result.Success();
        }

        Error? failure = null;

        int limit = (await configuration
                .ReadAsync(Settings.FactorTrustedDeviceFailureLimit, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        device.Failed(limit);
        await devices.RecordAsync(device, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// The browsers the account knows, as the account lists them.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The browsers, or the failure where the caller is nobody.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<DeviceSummary>>> ListAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<IReadOnlyList<DeviceSummary>>(Error.From(ErrorCodes.Denied));
        }

        IReadOnlyList<Device> standing = await devices
            .StandingOfAsync(subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<DeviceSummary>>(
        [
            .. standing.Select(device => new DeviceSummary(
                device.Id,
                device.Kind,
                device.Label,
                device.CreatedAt,
                device.LastUsedAt)),
        ]);
    }

    /// <summary>
    /// The person removed one browser: a trusted device is asked for the second
    /// factor again, a remembered browser faces the check again.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="id">Which browser.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure where it is not theirs to remove.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result> RemoveAsync(
        AccessContext context,
        DeviceId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // CONV-DESIGN-002 AC3: whose account is asking is the gate of an operation on
        // one's own browsers, asked before any browser is read.
        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        Device? device = await devices.FindAsync(id, cancellationToken).ConfigureAwait(false);

        if (device is null || device.Subject != subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        device.Revoke();
        await devices.RecordAsync(device, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Every trusted device of the account goes: the password changed, everything was
    /// signed out, the account was recovered, or it left the state that let it sign
    /// in at all.
    /// </summary>
    /// <param name="subject">Whose browsers.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of revoking them.</returns>
    public async ValueTask<Result> RevokeTrustAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Device> standing = await devices
            .StandingOfAsync(subject, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        foreach (Device device in standing.Where(device => device.Kind is DeviceKind.Trusted))
        {
            device.Revoke();
            await devices.RecordAsync(device, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private ValueTask<Result<(DeviceId Browser, OpaqueToken Token)>> RecordedAsync(
        SubjectId subject,
        DeviceDescription device,
        CancellationToken cancellationToken) =>
        KnownAsync(
            subject,
            DeviceKind.Remembered,
            device,
            Settings.DeviceVerificationLifetime,
            cancellationToken);

    private async ValueTask<Result<(DeviceId Browser, OpaqueToken Token)>> KnownAsync(
        SubjectId subject,
        DeviceKind kind,
        DeviceDescription device,
        DurationSetting lifetime,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan held = (await configuration.ReadAsync(lifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<(DeviceId, OpaqueToken)>(failure);
        }

        var token = OpaqueToken.Draw(randomness);
        var known = Device.Known(
            DeviceId.New(time),
            subject,
            kind,
            CredentialLabel.Of(device),
            time.GetUtcNow(),
            held);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await devices.AddAsync(known, token.Fingerprint(), cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success((known.Id, token));
    }

    private async ValueTask<bool> StandsAsync(
        SubjectId subject,
        [NeverLogged] string? presented,
        DeviceKind kind,
        CancellationToken cancellationToken)
    {
        Device? device = await OfAsync(subject, presented, kind, cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
        {
            return false;
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        device.Used(time.GetUtcNow());
        await devices.RecordAsync(device, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async ValueTask<Device?> OfAsync(
        SubjectId subject,
        [NeverLogged] string? presented,
        DeviceKind kind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presented))
        {
            return null;
        }

        Device? device = await devices
            .FindByFingerprintAsync(OpaqueToken.Of(presented).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        return device is not null
            && device.Subject == subject
            && device.Kind == kind
            && device.Stands(time.GetUtcNow())
            ? device
            : null;
    }
}
