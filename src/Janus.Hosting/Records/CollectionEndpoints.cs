using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Records;

/// <summary>
/// The collection session's endpoints of chapter 09 section 7a.
/// </summary>
/// <remarks>
/// Implements REC-COL-001, REC-AGE-001 and BFF-CSRF-005b. The session is the browser's,
/// so each endpoint maps its step through the library's own service and reads the
/// session from the pre-authentication cookie and from nowhere else: a browser that
/// does not carry it did not create this session, and it is refused whatever else it
/// knows (chapter 07, D-221).
/// </remarks>
internal static class CollectionEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Accepted = TypedResults.StatusCode(StatusCodes.Status202Accepted);

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapCollection(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/records/collection");

        _ = group.MapPost("/", BeginAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.RecordSignedIn))
            .Accepts<BeginCollectionRequest>(MediaTypeNames.Application.Json)
            .Produces<CollectionStateView>(StatusCodes.Status201Created);
        _ = group.MapGet("/", StateAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.RecordSignedIn, ErrorCodes.SessionExpired))
            .Produces<CollectionStateView>();
        _ = group.MapPut("/age", AgeAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RecordSignedIn, ErrorCodes.SessionExpired, ErrorCodes.CollectionIncomplete,
                    ErrorCodes.ProfileUnderage))
            .Accepts<CollectionAgeRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapPut("/email", EmailAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RecordSignedIn, ErrorCodes.SessionExpired, ErrorCodes.CollectionIncomplete,
                    ErrorCodes.IdentifierInvalid,
                    ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded))
            .Accepts<CollectionContactRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPut("/phone", PhoneAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RecordSignedIn, ErrorCodes.SessionExpired, ErrorCodes.CollectionIncomplete,
                    ErrorCodes.IdentifierInvalid,
                    ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded, ErrorCodes.SmsBalanceFloor))
            .Accepts<CollectionContactRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/verify", VerifyAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RecordSignedIn, ErrorCodes.SessionExpired, ErrorCodes.CodeInvalid,
                    ErrorCodes.CodeExpired,
                    ErrorCodes.Throttled))
            .Accepts<CollectionVerifyRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapPost("/abandon", AbandonAsync)
            .Declares(EndpointDeclaration.Answering())
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> BeginAsync(
        CollectionSessions collections,
        RequestSession browser,
        PreAuthenticationService contacts,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(context);

        BeginCollectionRequest request = await JsonBody
            .ReadAsync<BeginCollectionRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Age is not bool asksAge)
        {
            return Answers.Malformed("age");
        }

        if (browser.Context is null && browser.FirstContact is null)
        {
            return Gone(browser);
        }

        Error? failure = null;

        CollectionState state = (await collections
                .BeginAsync(browser.Context, asksAge, RequestOrigin.Language(context.Request), cancellationToken)
                .ConfigureAwait(false))
            .Match(begun => begun, error => Withheld<CollectionState>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        // BFF-CSRF-005b: the cookie is what makes "the browser that created this" a
        // checkable fact.
        await contacts
            .CarryAsync(
                browser.FirstContact ?? throw new InvalidOperationException(
                    "A collection is begun only for a browser that carries its first contact."),
                state.Id,
                state.ExpiresAt,
                cancellationToken)
            .ConfigureAwait(false);

        return Shown(state, StatusCodes.Status201Created);
    }

    private static async Task<IResult> StateAsync(
        CollectionSessions collections,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collections);

        return Carried(browser) is not CollectionSessionId session
            ? Gone(browser)
            : Answers.Of(
                await collections.StateAsync(session, cancellationToken).ConfigureAwait(false),
                state => Shown(state, StatusCodes.Status200OK));
    }

    private static async Task<IResult> AgeAsync(
        CollectionSessions collections,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(context);

        CollectionAgeRequest request = await JsonBody
            .ReadAsync<CollectionAgeRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not CollectionSessionId session)
        {
            return Gone(browser);
        }

        return request.DateOfBirth is not DateOnly born
            ? Answers.Malformed("dateOfBirth")
            : Answers.Of(
                await collections.AnswerAgeAsync(session, born, cancellationToken).ConfigureAwait(false),
                Nothing);
    }

    private static Task<IResult> EmailAsync(
        CollectionSessions collections,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken) =>
        StageAsync(IdentifierKind.Email, collections, browser, context, cancellationToken);

    private static Task<IResult> PhoneAsync(
        CollectionSessions collections,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken) =>
        StageAsync(IdentifierKind.Phone, collections, browser, context, cancellationToken);

    private static async Task<IResult> StageAsync(
        IdentifierKind kind,
        CollectionSessions collections,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(context);

        CollectionContactRequest request = await JsonBody
            .ReadAsync<CollectionContactRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not CollectionSessionId session)
        {
            return Gone(browser);
        }

        // REC-COL-001 AC3: accepted in the same bytes whoever holds the value.
        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await collections
                    .StageAsync(session, kind, value, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    private static async Task<IResult> VerifyAsync(
        CollectionSessions collections,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collections);
        ArgumentNullException.ThrowIfNull(context);

        CollectionVerifyRequest request = await JsonBody
            .ReadAsync<CollectionVerifyRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (Carried(browser) is not CollectionSessionId session)
        {
            return Gone(browser);
        }

        if (request.Kind is not IdentifierKind kind)
        {
            return Answers.Malformed("kind");
        }

        return request.Code is not { Length: > 0 } code
            ? Answers.Malformed("code")
            : Answers.Of(
                await collections
                    .VerifyAsync(session, kind, code, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> AbandonAsync(
        CollectionSessions collections,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collections);

        return Answers.Of(
            await collections.AbandonAsync(Carried(browser), cancellationToken).ConfigureAwait(false),
            Nothing);
    }

    private static CollectionSessionId? Carried(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Collection;
    }

    // `09` section 7a: a step taken from a browser signed in is refused as the beginning
    // of a collection is.
    private static IResult Gone(RequestSession browser) =>
        Answers.Refused(browser.Context is null ? ErrorCodes.SessionExpired : ErrorCodes.RecordSignedIn);

    private static JsonHttpResult<CollectionStateView> Shown(CollectionState state, int status) =>
        TypedResults.Json(
            CollectionStateView.Of(state),
            RecordsJson.Default.CollectionStateView,
            contentType: null,
            status);

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
