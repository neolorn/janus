using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Search;

/// <summary>
/// The staff search: the accounts and person records a query matches, over the index
/// this process serves.
/// </summary>
/// <param name="holder">The index this process serves, or why it serves none.</param>
/// <param name="admission">Where the reads of an organization are admitted.</param>
/// <param name="directory">Where the organization asked in is read.</param>
/// <param name="administrative">Which organization administers the deployment.</param>
/// <param name="subjects">Where the display names answered are read.</param>
/// <param name="windows">Where each caller's searches are counted.</param>
/// <param name="volume">Where the results a person is given are counted.</param>
/// <param name="configuration">Where <c>search.perminute</c> is read.</param>
/// <param name="work">The unit of work a search's count is written in.</param>
/// <param name="log">Where a search's count and duration are written.</param>
/// <param name="time">The clock a minute is read on.</param>
/// <remarks>
/// Implements IDN-SRCH-001, IDN-SRCH-002, IDN-SRCH-003 and LIB-API-005 (D-256). The
/// gate step refuses a system principal, then reads the organization asked in, then
/// admits the kinds asked as one admission, under <c>contact:read</c> for accounts and
/// <c>record:read</c> for records, held there or in the administrative organization. No
/// audit row is written; a refusal is recorded as every refusal is. A result whose
/// subject was erased or taken down since the index last heard of it is not answered,
/// since its name is read at the search.
/// </remarks>
internal sealed class SearchService(
    SearchIndexHolder holder,
    IReadAdmission admission,
    IOrganizationDirectory directory,
    IAdministrativeOrganization administrative,
    ISearchSubjects subjects,
    ISearchWindows windows,
    IReadVolume volume,
    IConfigurationStore configuration,
    IUnitOfWork work,
    ISearchLog log,
    TimeProvider time) : ISearch
{
    private const int MostLimit = 50;

    private const int LongestQuery = 64;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The context or the query is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<SearchResult>>> SearchAsync(
        AccessContext context,
        SearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        long began = time.GetTimestamp();

        // IDN-SRCH-002: a system principal, a job's included, may not search.
        if (context.IsSystem || context.Acting is not SubjectId searcher)
        {
            return Refused(Error.From(ErrorCodes.Denied));
        }

        if ((query.Kinds & ~(SearchKinds.Accounts | SearchKinds.Records)) != SearchKinds.None)
        {
            return Refused(Malformed("kinds"));
        }

        if (query.Limit is < 1 or > MostLimit)
        {
            return Refused(Malformed("limit"));
        }

        // AUTHZ-SCOPE-001: the organization asked in is resolved where the gate is asked.
        if (await ScopeOfAsync(query.Organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Refused(Error.From(ErrorCodes.OrganizationNotFound));
        }

        SearchKinds asked = query.Kinds is SearchKinds.None ? SearchKinds.Accounts | SearchKinds.Records : query.Kinds;

        Result<IReadOnlyList<Permission>> readable = await admission
            .ReadableAsync(context, Asked(asked), query.Organization, cancellationToken)
            .ConfigureAwait(false);

        if (readable.Match<Error?>(_ => null, error => error) is Error refused)
        {
            return Refused(refused);
        }

        IReadOnlyList<Permission> admitted = readable.Match(permissions => permissions, _ => []);
        string normalised = SearchForm.Of(query.Text);

        if (normalised.Length is 0 or > LongestQuery)
        {
            return Refused(Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement("q")));
        }

        Result<SearchIndex> served = holder.Current();

        if (served.Match<Error?>(_ => null, error => error) is Error unavailable)
        {
            return Refused(unavailable);
        }

        if (await ThrottledAsync(searcher, cancellationToken).ConfigureAwait(false) is Error throttled)
        {
            return Refused(throttled);
        }

        OrganizationId? administering = await administrative.FindAsync(cancellationToken).ConfigureAwait(false);
        var scope = new IndexScope(
            query.Organization == administering ? null : query.Organization,
            admitted.Contains(Permissions.ContactRead),
            admitted.Contains(Permissions.RecordRead));

        IReadOnlyList<IndexHit> hits = served
            .Match(index => index, _ => throw new InvalidOperationException("The index was read above."))
            .Find(normalised.Split(' '), scope, query.Limit);

        IReadOnlyDictionary<SubjectId, string?> names = await subjects
            .NamesAsync([.. hits.Select(hit => hit.Subject)], cancellationToken)
            .ConfigureAwait(false);

        List<SearchResult> results =
        [
            .. hits
                .Where(hit => names.ContainsKey(hit.Subject))
                .Select(hit => new SearchResult(hit.Subject, hit.Kind, names[hit.Subject], hit.Matched)),
        ];

        if ((await volume.ReturnedAsync(context, results.Count, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error uncounted)
        {
            return Refused(uncounted);
        }

        log.Searched(results.Count, time.GetElapsedTime(began));

        return Result.Success<IReadOnlyList<SearchResult>>(results);
    }

    private static Result<IReadOnlyList<SearchResult>> Refused(Error error) =>
        Result.Failure<IReadOnlyList<SearchResult>>(error);

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static List<Permission> Asked(SearchKinds kinds)
    {
        var permissions = new List<Permission>(2);

        if (kinds.HasFlag(SearchKinds.Accounts))
        {
            permissions.Add(Permissions.ContactRead);
        }

        if (kinds.HasFlag(SearchKinds.Records))
        {
            permissions.Add(Permissions.RecordRead);
        }

        return permissions;
    }

    // The organization asked in, or nothing where the deployment holds none or it was
    // erased.
    private async ValueTask<OrganizationId?> ScopeOfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is { ErasedAt: null }
            ? organization
            : null;

    // IDN-SRCH-002: one count a caller and a minute, across the deployment, refused once
    // it passes search.perminute until the next minute begins.
    private async ValueTask<Error?> ThrottledAsync(SubjectId searcher, CancellationToken cancellationToken)
    {
        int limit = (await configuration.ReadAsync(Settings.SearchPerMinute, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        DateTimeOffset now = time.GetUtcNow();
        var minute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Match<Error?>(_ => null, error => error)
            is Error notBegun)
        {
            return notBegun;
        }

        int counted = await windows.CountAsync(searcher, minute, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false)).Match<Error?>(() => null, error => error)
            is Error notCommitted)
        {
            return notCommitted;
        }

        return counted > limit ? Error.Throttled(minute + Window) : null;
    }
}
