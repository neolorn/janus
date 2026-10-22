using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Privacy.Consents;
using Janus.Privacy.Documents;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Whether the cookies the deployment sets are those the current version of the privacy
/// notice lists, raising <c>degradation</c> under <c>notice.cookies</c> where they are
/// not.
/// </summary>
/// <param name="documents">Where the current version of the notice is read.</param>
/// <param name="cookies">The cookies the deployment sets now.</param>
/// <param name="alerts">Where a difference is raised.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements PRIV-CONS-006a and chapter 10 section 5.23 (D-203). A cookie differs by
/// its name, purpose, lifetime or readability by a third party; the notice tells a
/// reader what the deployment sets, so a difference is raised until a version listing
/// the cookies is published. A deployment with no version published lists nothing
/// yet, and raises nothing.
/// </remarks>
internal sealed class NoticeCookieCheck(
    ILegalDocumentStore documents,
    IDeploymentCookies cookies,
    IAlertChannels alerts,
    TimeProvider time)
{
    /// <summary>
    /// The scope of chapter 10 section 5.23 a difference is raised under.
    /// </summary>
    public const string Scope = "notice.cookies";

    /// <summary>
    /// Compares the cookies the deployment sets with those the current notice lists,
    /// and raises the difference, named by cookie, where there is one.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure where the cookies could not be read or the alert raised.</returns>
    public async ValueTask<Result> ComparedAsync(CancellationToken cancellationToken)
    {
        if (await documents.CurrentAsync(ConsentService.Notice, cancellationToken).ConfigureAwait(false)
            is not DocumentVersion current)
        {
            return Result.Success();
        }

        Result<IReadOnlyList<NoticeCookie>> read = await cookies.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (read.Match<Error?>(_ => null, error => error) is Error unread)
        {
            return Result.Failure(unread);
        }

        var listed = current.Cookies.ToDictionary(
            cookie => cookie.Name,
            StringComparer.Ordinal);
        var set = read
            .Match(now => now, _ => [])
            .ToDictionary(cookie => cookie.Name, StringComparer.Ordinal);

        string[] added = [.. set.Keys.Where(name => !listed.ContainsKey(name)).Order(StringComparer.Ordinal)];
        string[] removed = [.. listed.Keys.Where(name => !set.ContainsKey(name)).Order(StringComparer.Ordinal)];
        string[] changed =
        [
            .. set.Values
                .Where(cookie => listed.TryGetValue(cookie.Name, out NoticeCookie? was) && was != cookie)
                .Select(cookie => cookie.Name)
                .Order(StringComparer.Ordinal),
        ];

        if (added.Length is 0 && removed.Length is 0 && changed.Length is 0)
        {
            return Result.Success();
        }

        return await alerts
            .RaiseAsync(
                Alerts.Scoped(
                    AlertCondition.Degradation,
                    Scope,
                    time.GetUtcNow(),
                    new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
                    {
                        ["added"] = JsonSerializer.SerializeToElement(added),
                        ["removed"] = JsonSerializer.SerializeToElement(removed),
                        ["changed"] = JsonSerializer.SerializeToElement(changed),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
