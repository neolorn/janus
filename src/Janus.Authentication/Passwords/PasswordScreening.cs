using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Passwords;

/// <summary>
/// Screens a password against the sources the deployment rejects on, at set and at
/// change. A source that cannot answer refuses the operation; nothing is ever
/// accepted unscreened.
/// </summary>
/// <param name="corpus">Where the compromised-password corpus is read from.</param>
/// <param name="words">The word list, where the deployment rejects on one.</param>
/// <param name="configuration">Where the deployment's choices are read from.</param>
/// <param name="log">Where a fall back to another corpus is recorded.</param>
/// <param name="alerts">Where a fall back to another corpus is raised.</param>
/// <param name="time">When a fall back happened.</param>
/// <remarks>
/// Implements AUTH-PASS-004, INT-PWD-001, INT-PWD-002, INT-PWD-003 and OPS-OBS-002. A
/// fall back is raised as <c>degradation</c> before the offline corpus is asked, and
/// one that cannot be raised refuses the operation: screening never degrades
/// unseen.
/// </remarks>
internal sealed class PasswordScreening(
    ILeakedPasswordCorpus corpus,
    IWordList words,
    IConfigurationStore configuration,
    IScreeningLog log,
    IAlertChannels alerts,
    TimeProvider time)
{
    private const int PrefixLength = 5;

    private const string Fallback = "password.blocklist.fallback";

    /// <summary>
    /// Screens a password.
    /// </summary>
    /// <param name="password">The password, in UTF-8. The caller clears it.</param>
    /// <param name="ownWords">
    /// The person's own identifiers and profile fields, and the service name, which
    /// the context source rejects on where the deployment has enabled it.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success where every enabled source passed it, the blocklist failure where one
    /// matched, or the screening failure where a source could not answer.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> ScreenAsync(
        [NeverLogged] byte[] password,
        IReadOnlyCollection<string> ownWords,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(ownWords);

        Error? failure = null;

        IReadOnlySet<BlocklistRejectionSource> enabled =
            (await configuration.ReadAsync(Settings.PasswordBlocklistSources, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlySet<BlocklistRejectionSource>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        Result leaked = await LeakedAsync(password, cancellationToken).ConfigureAwait(false);
        Error? refused = leaked.Match<Error?>(() => null, error => error);

        if (refused is not null)
        {
            return Result.Failure(refused);
        }

        string text = Encoding.UTF8.GetString(password);

        if (enabled.Contains(BlocklistRejectionSource.Dictionary))
        {
            bool listed = (await words.MatchesAsync(text, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<bool>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure(Error.From(ErrorCodes.ScreeningUnavailable));
            }

            if (listed)
            {
                return Refused();
            }
        }

        return enabled.Contains(BlocklistRejectionSource.Context)
            && PasswordAdvice.Appearing(text, ownWords).Any()
                ? Refused()
                : Result.Success();
    }

    /// <summary>
    /// Whether a password the account already holds now matches one of the person's
    /// own words. Asked at a sign-in, where a match is a prompt to change and never a
    /// refusal (AUTH-PASS-004).
    /// </summary>
    /// <param name="password">The password, in UTF-8. The caller clears it.</param>
    /// <param name="ownWords">
    /// The person's own identifiers and profile fields, and the service name.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether one appears in it, which is always false where the deployment does not
    /// reject on them.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<bool>> ContextMatchesAsync(
        [NeverLogged] byte[] password,
        IReadOnlyCollection<string> ownWords,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(ownWords);

        Error? failure = null;

        IReadOnlySet<BlocklistRejectionSource> enabled =
            (await configuration.ReadAsync(Settings.PasswordBlocklistSources, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlySet<BlocklistRejectionSource>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        return Result.Success(
            enabled.Contains(BlocklistRejectionSource.Context)
            && PasswordAdvice.Appearing(Encoding.UTF8.GetString(password), ownWords).Any());
    }

    private static Result Refused() => Result.Failure(Error.From(ErrorCodes.PasswordBlocklisted));

    private static Dictionary<string, JsonElement> FellBack(BlocklistSource configured) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["configured"] = JsonSerializer.SerializeToElement(WrittenName.Of(configured)),
            ["used"] = JsonSerializer.SerializeToElement(WrittenName.Of(BlocklistSource.Offline)),
        };

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<Result> LeakedAsync([NeverLogged] byte[] password, CancellationToken cancellationToken)
    {
        // INT-PWD-001: the prefix is the first five upper-case hexadecimal characters
        // of the hash, and the remaining thirty-five are matched here.
#pragma warning disable CA5350 // The corpus is published as SHA-1 ranges (INT-PWD-001); the value is a lookup key and never a security claim.
        string hash = Convert.ToHexString(SHA1.HashData(password));
#pragma warning restore CA5350

        Error? failure = null;

        BlocklistSource asked =
            (await configuration.ReadAsync(Settings.PasswordBlocklistSource, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<BlocklistSource>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        IReadOnlySet<string>? range = await AskAsync(asked, hash, cancellationToken).ConfigureAwait(false);

        if (range is null && asked is not BlocklistSource.Offline)
        {
            log.Degraded(asked, BlocklistSource.Offline);

            Result raised = await alerts
                .RaiseAsync(
                    Alerts.Of(AlertCondition.Degradation, Fallback, time.GetUtcNow(), FellBack(asked)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (raised.Match(() => (Error?)null, error => error) is Error unraised)
            {
                return Result.Failure(unraised);
            }

            range = await AskAsync(BlocklistSource.Offline, hash, cancellationToken).ConfigureAwait(false);
        }

        if (range is null)
        {
            return Result.Failure(Error.From(ErrorCodes.ScreeningUnavailable));
        }

        return range.Contains(hash[PrefixLength..])
            ? Refused()
            : Result.Success();
    }

    private async ValueTask<IReadOnlySet<string>?> AskAsync(
        BlocklistSource source,
        [NeverLogged] string hash,
        CancellationToken cancellationToken) =>
        (await corpus.RangeAsync(source, hash[..PrefixLength], cancellationToken).ConfigureAwait(false))
            .Match<IReadOnlySet<string>?>(value => value, _ => null);
}
