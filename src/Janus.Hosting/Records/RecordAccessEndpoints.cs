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
/// The record access session's endpoints of chapter 09 section 7a.
/// </summary>
/// <remarks>
/// Implements REC-ACC-001 and BFF-CSRF-005a. The session is the browser's, so each
/// endpoint maps its step through the library's own service and reads the session from
/// the pre-authentication cookie and from nowhere else: a browser that does not carry it
/// did not ask for this session, and it is refused whatever else it knows (chapter 07,
/// D-221).
/// </remarks>
internal static class RecordAccessEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Accepted = TypedResults.StatusCode(StatusCodes.Status202Accepted);

    // Chapter 09 section 7a: a reference is 1 to 64 characters after trimming.
    private const int ReferenceLength = 64;

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapRecordAccess(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/records/access");

        _ = group.MapPost("/", AskAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RecordSignedIn, ErrorCodes.SessionExpired, ErrorCodes.IdentifierInvalid,
                    ErrorCodes.Throttled, ErrorCodes.RestrictionExceeded))
            .Accepts<RecordAccessRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/verify", VerifyAsync)
            .Declares(EndpointDeclaration
                .Answering(ErrorCodes.CodeInvalid, ErrorCodes.CodeExpired, ErrorCodes.Throttled))
            .Accepts<RecordAccessVerifyRequest>(MediaTypeNames.Application.Json)
            .Produces<RecordAccessStateView>();
        _ = group.MapGet("/", StateAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.SessionExpired))
            .Produces<RecordAccessStateView>();
        _ = group.MapPost("/reference", ReferenceAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.SessionExpired, ErrorCodes.Throttled))
            .Accepts<RecordAccessReferenceRequest>(MediaTypeNames.Application.Json)
            .Produces<RecordAccessStateView>();
        _ = group.MapPost("/end", EndAsync)
            .Declares(EndpointDeclaration.Answering())
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> AskAsync(
        RecordAccessSessions access,
        RequestSession browser,
        PreAuthenticationService contacts,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(context);

        RecordAccessRequest request = await JsonBody
            .ReadAsync<RecordAccessRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Kind is not IdentifierKind kind)
        {
            return Answers.Malformed("kind");
        }

        if (request.Value is not { Length: > 0 } value)
        {
            return Answers.Malformed("value");
        }

        if (browser.Context is null && browser.FirstContact is null)
        {
            return Answers.Refused(ErrorCodes.SessionExpired);
        }

        Error? failure = null;

        RecordAccessSession asked = (await access
                .AskAsync(
                    browser.Context,
                    browser.Access,
                    kind,
                    value,
                    RequestOrigin.Language(context.Request),
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(session => session, error => Withheld<RecordAccessSession>(error, ref failure));

        if (failure is not null)
        {
            return Answers.Refused(failure);
        }

        // BFF-CSRF-005a: the cookie is what makes "the browser that asked" a checkable
        // fact, and the code opens the session in that browser alone.
        await contacts
            .CarryAsync(
                browser.FirstContact ?? throw new InvalidOperationException(
                    "A record access code is asked only by a browser that carries its first contact."),
                asked.Id,
                asked.ExpiresAt,
                cancellationToken)
            .ConfigureAwait(false);

        // REC-ACC-001 AC1: answered in the same bytes whatever is held.
        return Accepted;
    }

    private static async Task<IResult> VerifyAsync(
        RecordAccessSessions access,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordAccessVerifyRequest request = await JsonBody
            .ReadAsync<RecordAccessVerifyRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Code is not { Length: > 0 } code)
        {
            return Answers.Malformed("code");
        }

        Error? failure = (await access
                .VerifyAsync(browser.Access, code, RequestOrigin.Source(context.Request), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);

        return failure is not null
            ? Answers.Refused(failure)
            : await ShownAsync(access, browser, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> StateAsync(
        RecordAccessSessions access,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(browser);

        return await ShownAsync(access, browser, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> ReferenceAsync(
        RecordAccessSessions access,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordAccessReferenceRequest request = await JsonBody
            .ReadAsync<RecordAccessReferenceRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Reference?.Trim() is not { Length: > 0 and <= ReferenceLength } reference)
        {
            return Answers.Malformed("reference");
        }

        Error? failure = (await access
                .ReferenceAsync(browser.Access, reference, RequestOrigin.Source(context.Request), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);

        return failure is not null
            ? Answers.Refused(failure)
            : await ShownAsync(access, browser, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> EndAsync(
        RecordAccessSessions access,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await access.EndAsync(browser.Access, cancellationToken).ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ShownAsync(
        RecordAccessSessions access,
        RequestSession browser,
        CancellationToken cancellationToken) =>
        Answers.Of(
            await access.StateAsync(browser.Access, cancellationToken).ConfigureAwait(false),
            state => TypedResults.Json(RecordAccessStateView.Of(state), RecordsJson.Default.RecordAccessStateView));

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
