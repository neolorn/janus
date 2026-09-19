using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Passwords;

/// <summary>
/// The compromised-password corpus, over the range API and the two files a deployment
/// may hold.
/// </summary>
/// <param name="requests">The client the range API is asked over.</param>
/// <param name="configuration">Where the deployment's choices are read from.</param>
/// <param name="time">The clock the corpus age is judged against.</param>
/// <param name="directory">The directory the corpus files are read from.</param>
/// <remarks>
/// Implements INT-PWD-001, INT-PWD-002 and INT-PWD-003. One prefix goes out and a
/// range comes back, so neither the password nor its full hash leaves the deployment;
/// a source that cannot answer returns the screening failure, and screening treats
/// that as a refusal rather than as a pass.
/// </remarks>
internal sealed class LeakedPasswordCorpus(
    HttpClient requests,
    IConfigurationStore configuration,
    TimeProvider time,
    string directory) : ILeakedPasswordCorpus
{
    /// <summary>
    /// The directory beside the application that a deployment holds its corpus in.
    /// </summary>
    public const string Directory = "janus-corpus";

    /// <summary>The curated list the package carries.</summary>
    public const string OfflineFile = "leaked-passwords.txt";

    /// <summary>The corpus a deployment maintains itself.</summary>
    public const string SelfHostedFile = "self-hosted-passwords.txt";

    /// <summary>
    /// The path the provider D-011 names serves ranges under. Every prefix is a
    /// relative address against it.
    /// </summary>
    public static readonly Uri Provider = new("https://api.pwnedpasswords.com/range/");

    private const char Separator = ':';

    private readonly CorpusFile _offline = new(Path.Combine(directory, OfflineFile));
    private readonly CorpusFile _selfHosted = new(Path.Combine(directory, SelfHostedFile));

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlySet<string>>> RangeAsync(
        BlocklistSource source,
        string prefix,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (source is BlocklistSource.RangeApi)
        {
            return await AskedAsync(prefix, cancellationToken).ConfigureAwait(false);
        }

        Error? failure = null;

        TimeSpan maximumAge =
            (await configuration.ReadAsync(Settings.PasswordBlocklistCorpusMaxAge, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlySet<string>>(failure);
        }

        CorpusFile file = source is BlocklistSource.SelfHosted ? _selfHosted : _offline;
        IReadOnlySet<string>? range = await file
            .RangeAsync(prefix, time.GetUtcNow(), maximumAge, cancellationToken)
            .ConfigureAwait(false);

        return range is null ? Unavailable() : Result.Success(range);
    }

    private static Result<IReadOnlySet<string>> Unavailable() =>
        Result.Failure<IReadOnlySet<string>>(Error.From(ErrorCodes.ScreeningUnavailable));

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static async ValueTask<IReadOnlySet<string>> ReadAsync(
        HttpResponseMessage answer,
        CancellationToken cancellationToken)
    {
        HashSet<string> range = new(StringComparer.Ordinal);

        using StreamReader reading = new(
            await answer.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));

        while (await reading.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
        {
            // The provider publishes the suffix and the times it has been seen; the
            // count is no rejection rule here, so it is read past (INT-PWD-001).
            int end = line.IndexOf(Separator, StringComparison.Ordinal);
            string suffix = (end < 0 ? line : line[..end]).Trim();

            if (suffix.Length is not 0)
            {
                _ = range.Add(suffix.ToUpperInvariant());
            }
        }

        return range;
    }

    private async ValueTask<Result<IReadOnlySet<string>>> AskedAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage answer = await requests
                .GetAsync(
                    new Uri(prefix.ToUpperInvariant(), UriKind.Relative),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            return answer.IsSuccessStatusCode
                ? Result.Success(await ReadAsync(answer, cancellationToken).ConfigureAwait(false))
                : Unavailable();
        }
        catch (HttpRequestException)
        {
            return Result.Failure<IReadOnlySet<string>>(Error.From(ErrorCodes.ScreeningUnavailable));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<IReadOnlySet<string>>(Error.From(ErrorCodes.ScreeningUnavailable));
        }
    }
}
