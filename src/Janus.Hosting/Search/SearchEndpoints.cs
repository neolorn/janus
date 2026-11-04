using System;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Search;

/// <summary>
/// The staff search's route.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-001 to IDN-SRCH-003, LIB-API-005 and chapter 09 section 8a
/// (D-256). It is a <c>POST</c>, so the query never reaches an address or an access log.
/// </remarks>
internal static class SearchEndpoints
{
    private const int DefaultLimit = 20;

    /// <summary>
    /// Mounts <c>POST /admin/search</c>.
    /// </summary>
    /// <param name="endpoints">Where it is to be mounted.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapSearch(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapPost("/admin/search", SearchAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.RequestInvalid, ErrorCodes.Denied,
                    ErrorCodes.OrganizationNotFound, ErrorCodes.Throttled, ErrorCodes.SearchUnavailable))
            .Accepts<SearchRequest>(MediaTypeNames.Application.Json)
            .Produces<SearchResultsView>();

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        ISearch search,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        SearchRequest request = await JsonBody
            .ReadAsync<SearchRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Organization is not Guid organization)
        {
            return Answers.Malformed("organization");
        }

        if (request.Q is not string query)
        {
            return Answers.Malformed("q");
        }

        SearchKinds kinds = SearchKinds.None;

        foreach (string? kind in request.Kinds ?? [])
        {
            switch (kind)
            {
                case "account":
                    kinds |= SearchKinds.Accounts;
                    break;
                case "record":
                    kinds |= SearchKinds.Records;
                    break;
                default:
                    return Answers.Malformed("kinds");
            }
        }

        return Answers.Of(
            await search
                .SearchAsync(
                    browser.Asking,
                    new SearchQuery(new OrganizationId(organization), query, kinds, request.Limit ?? DefaultLimit),
                    cancellationToken)
                .ConfigureAwait(false),
            results => TypedResults.Json(
                new SearchResultsView([.. results.Select(SearchResultView.Of)]),
                SearchJson.Default.SearchResultsView,
                contentType: null,
                StatusCodes.Status200OK));
    }
}
