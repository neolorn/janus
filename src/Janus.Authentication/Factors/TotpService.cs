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
/// Enrolling a code generator and presenting a code from one.
/// </summary>
/// <param name="authenticators">Where enrolled credentials are read and written.</param>
/// <param name="configuration">Where the drift tolerance is read from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a shared secret is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-005, AUTH-FACT-006 and AUTH-FACT-007. An enrolment becomes
/// usable only once one valid code has been presented, so a mis-scanned code does not
/// lock its owner out.
/// </remarks>
internal sealed class TotpService(
    IAuthenticatorStore authenticators,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Begins an enrolment: the secret is drawn and recorded, and the credential is
    /// not usable until a code from it is presented.
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credential and the secret to show once.</returns>
    public async ValueTask<Result<TotpEnrolment>> BeginAsync(
        SubjectId subject,
        CredentialLabel label,
        CancellationToken cancellationToken)
    {
        byte[] secret = TotpCodes.Draw(randomness);
        var enrolling = Authenticator.EnrollingTotp(
            AuthenticatorId.New(time),
            subject,
            label,
            secret,
            time.GetUtcNow());

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await authenticators.AddAsync(enrolling, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new TotpEnrolment(enrolling.Id, secret));
    }

    /// <summary>
    /// Confirms an enrolment with one valid code, which is what makes the credential
    /// usable.
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <param name="id">Which credential.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the failure where the credential is not theirs to confirm or the
    /// code is not one of its own.
    /// </returns>
    public async ValueTask<Result> ConfirmAsync(
        SubjectId subject,
        AuthenticatorId id,
        string code,
        CancellationToken cancellationToken)
    {
        Authenticator? enrolling = await authenticators.FindAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (enrolling is null || enrolling.Subject != subject || enrolling.Totp is null)
        {
            return Result.Failure(Error.From(ErrorCodes.FactorRejected));
        }

        DateTimeOffset now = time.GetUtcNow();
        long? step = TotpCodes.Accepts(
            enrolling.Totp,
            code,
            now,
            await DriftAsync(cancellationToken).ConfigureAwait(false));

        if (step is null)
        {
            return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        enrolling.Consumed(step.Value);
        enrolling.Confirm(now);
        await authenticators.RecordAsync(enrolling, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Abandons an enrolment that was never confirmed, leaving no credential behind.
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <param name="id">Which credential.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the failure where the credential is not theirs or was confirmed,
    /// a confirmed credential being removed through the credential list.
    /// </returns>
    public async ValueTask<Result> AbandonAsync(
        SubjectId subject,
        AuthenticatorId id,
        CancellationToken cancellationToken)
    {
        Authenticator? enrolling = await authenticators.FindAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (enrolling is null || enrolling.Subject != subject || enrolling.Confirmed)
        {
            return Result.Failure(Error.From(ErrorCodes.FactorRejected));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await authenticators.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Presents a code against the account's usable code generators.
    /// </summary>
    /// <param name="subject">Whose credential.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential the code belonged to, or the failure where no usable generator
    /// of the account accepts it. A code whose step has been spent is refused as
    /// replayed, so the person is told to wait for the next one rather than that
    /// their code is wrong.
    /// </returns>
    public async ValueTask<Result<AuthenticatorId>> PresentAsync(
        SubjectId subject,
        string code,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Authenticator> held =
            await authenticators.OfAsync(subject, cancellationToken).ConfigureAwait(false);
        List<Authenticator> generators =
            [.. held.Where(credential => credential.IsUsable && credential.Totp is not null)];

        DateTimeOffset now = time.GetUtcNow();
        int drift = await DriftAsync(cancellationToken).ConfigureAwait(false);

        foreach (Authenticator generator in generators)
        {
            if (TotpCodes.Accepts(generator.Totp!, code, now, drift) is not long step)
            {
                continue;
            }

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            generator.Consumed(step);
            generator.Used(now);
            await authenticators.RecordAsync(generator, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(generator.Id);
        }

        return Result.Failure<AuthenticatorId>(Error.From(Refusal(generators, code, now, drift)));
    }

    // A code that would be valid but for its step having been spent is a replay, and
    // saying so is what tells the person to wait rather than to look again.
    private static ErrorCode Refusal(
        IReadOnlyCollection<Authenticator> generators,
        string code,
        DateTimeOffset now,
        int drift) =>
        generators.Any(generator =>
            TotpCodes.Accepts(generator.Totp! with { ConsumedStep = null }, code, now, drift) is not null)
            ? ErrorCodes.CodeReplayed
            : ErrorCodes.CodeInvalid;

    private async ValueTask<int> DriftAsync(CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(Settings.FactorTotpDrift, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.FactorTotpDrift.Default);
}
