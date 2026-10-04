using System;
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
/// Enrolling a code generator and presenting a code from one.
/// </summary>
/// <param name="authenticators">Where enrolled credentials are read and written.</param>
/// <param name="passwords">Where the account's password is read.</param>
/// <param name="configuration">Where the drift tolerance is read from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a shared secret is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-005, AUTH-FACT-006, AUTH-FACT-007 and the second-step rule
/// of AUTH-FACT-002b. An enrolment becomes usable only once one valid code has been
/// presented, so a mis-scanned code does not lock its owner out.
/// </remarks>
internal sealed class TotpService(
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
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

        if (SecondStep.Is(enrolling.Factor)
            && !await SecondStep.AvailableAsync(passwords, subject, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<TotpEnrolment>(Error.From(ErrorCodes.FactorPasswordRequired));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<TotpEnrolment>(notBegun);
        }

        await authenticators.AddAsync(enrolling, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<TotpEnrolment>(notCommitted);
        }

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
        [NeverLogged] string code,
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        enrolling.Consumed(step.Value);
        enrolling.Confirm(now);
        await authenticators.RecordAsync(enrolling, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <summary>
    /// The time step a code of a secret is accepted for now, within the drift the
    /// deployment tolerates, writing nothing: a registration session confirms the
    /// generator it staged by it (REG-SESS-006).
    /// </summary>
    /// <param name="material">The secret, and the last step a code of it was accepted for.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The step, or nothing where the code is not one of the secret.</returns>
    public async ValueTask<long?> AcceptsAsync(
        TotpMaterial material,
        [NeverLogged] string code,
        CancellationToken cancellationToken) =>
        TotpCodes.Accepts(
            material,
            code,
            time.GetUtcNow(),
            await DriftAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// What the authenticator app is given for an enrolment just begun: the secret as
    /// text to type and as the address a QR code carries.
    /// </summary>
    /// <param name="credential">Which enrolment.</param>
    /// <param name="secret">The shared secret.</param>
    /// <param name="account">What the app shows beside the code.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The enrolment as it is shown once.</returns>
    public async ValueTask<GeneratorEnrolment> ShownAsync(
        AuthenticatorId credential,
        ReadOnlyMemory<byte> secret,
        string account,
        CancellationToken cancellationToken)
    {
        byte[] shown = secret.ToArray();

        try
        {
            string text = TotpCodes.Text(shown);

            return new GeneratorEnrolment(
                credential,
                text,
                TotpCodes.Address(
                    await IssuerAsync(cancellationToken).ConfigureAwait(false),
                    account,
                    text));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shown);
        }
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await authenticators.RemoveAsync(id, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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
        [NeverLogged] string code,
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
            if (TotpCodes.Accepts(generator.Totp!, code, now, drift) is null)
            {
                continue;
            }

            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(_ => null, error => error) is Error notBegun)
            {
                return Result.Failure<AuthenticatorId>(notBegun);
            }

            // D-166 X3: the step is judged again on the row under its lock, so the same
            // code presented twice at once is accepted once and replayed once.
            Authenticator? locked = await authenticators.FindForUpdateAsync(generator.Id, cancellationToken)
                .ConfigureAwait(false);
            long? consumed = locked is { IsUsable: true, Totp: not null }
                ? TotpCodes.Accepts(locked.Totp, code, now, drift)
                : null;

            if (consumed is not long accepted)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure<AuthenticatorId>(Error.From(Refusal(generators, code, now, drift)));
            }

            locked!.Consumed(accepted);
            locked.Used(now);
            await authenticators.RecordAsync(locked, cancellationToken).ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommitted)
            {
                return Result.Failure<AuthenticatorId>(notCommitted);
            }

            return Result.Success(generator.Id);
        }

        return Result.Failure<AuthenticatorId>(Error.From(Refusal(generators, code, now, drift)));
    }

    // A code that would be valid but for its step having been spent is a replay, and
    // saying so is what tells the person to wait rather than to look again.
    private static ErrorCode Refusal(
        IReadOnlyCollection<Authenticator> generators,
        [NeverLogged] string code,
        DateTimeOffset now,
        int drift) =>
        generators.Any(generator =>
            TotpCodes.Accepts(generator.Totp! with { ConsumedStep = null }, code, now, drift) is not null)
            ? ErrorCodes.CodeReplayed
            : ErrorCodes.CodeInvalid;

    // The service name is a key the deployment names only where the context source is
    // on, so one never named is no name for the authenticator app to show.
    private async ValueTask<string> IssuerAsync(CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(Settings.ServiceName, cancellationToken).ConfigureAwait(false))
            .Match(
                value => value,
                error => error.Code == ErrorCodes.StartupDeclarationMissing
                    ? string.Empty
                    : throw new InvalidOperationException(error.Code.ToString()));

    private async ValueTask<int> DriftAsync(CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(Settings.FactorTotpDrift, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));
}
