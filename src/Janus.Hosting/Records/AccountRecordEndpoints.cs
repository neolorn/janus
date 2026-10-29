using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Records;

/// <summary>
/// The account's person-record endpoints of chapter 09 section 7a: the records attached
/// to it, the offer a proof brings, the code that finds them, and the attachment.
/// </summary>
/// <remarks>
/// Implements REC-ATT-001 and API-CONV-001. Each maps an operation of
/// <see cref="IPersonRecords"/> in the account's own session; who is asking is what the
/// session resolution stage established and nothing an endpoint reads from the request.
/// </remarks>
internal static class AccountRecordEndpoints
{
    private static readonly IResult Accepted = TypedResults.StatusCode(StatusCodes.Status202Accepted);

    // Chapter 09 section 7a: a reference is 1 to 64 characters after trimming.
    private const int ReferenceLength = 64;

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAccountRecords(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/account/records");

        _ = SessionRequired.On(group.MapGet("/", ListAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<IReadOnlyList<AttachedRecordView>>();
        _ = SessionRequired.On(group.MapGet("/offer", OfferAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<RecordOfferView>();
        _ = SessionRequired.On(group.MapPost("/find", FindAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.IdentifierInvalid, ErrorCodes.Throttled,
                    ErrorCodes.RestrictionExceeded))
            .Accepts<RecordFindCodeRequest>(MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status202Accepted);
        _ = SessionRequired.On(group.MapPost("/find/verify", VerifyAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.CodeInvalid, ErrorCodes.CodeExpired,
                    ErrorCodes.Throttled))
            .Accepts<RecordFindVerifyRequest>(MediaTypeNames.Application.Json)
            .Produces<RecordOfferView>();
        _ = SessionRequired.On(group.MapPost("/attach", AttachAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Restricted, ErrorCodes.AccountStateConflict))
            .Produces<RecordsAttachedView>();
        _ = SessionRequired.On(group.MapPost("/attach/reference", AttachByReferenceAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Restricted, ErrorCodes.RecordNoOffer,
                    ErrorCodes.Throttled, ErrorCodes.AccountStateConflict))
            .Accepts<RecordAttachReferenceRequest>(MediaTypeNames.Application.Json)
            .Produces<RecordsAttachedView>();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        IPersonRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await records.ListAttachedAsync(browser.Asking, cancellationToken).ConfigureAwait(false),
            attached => TypedResults.Json<IReadOnlyList<AttachedRecordView>>(
                [.. attached.Select(AttachedRecordView.Of)],
                RecordsJson.Default.IReadOnlyListAttachedRecordView));
    }

    private static async Task<IResult> OfferAsync(
        IPersonRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Offered(
            await records.ReadOfferAsync(browser.Asking, browser.Required.Id, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> FindAsync(
        IPersonRecords records,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordFindCodeRequest request = await JsonBody
            .ReadAsync<RecordFindCodeRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Identifier is not Guid identifier)
        {
            return Answers.Malformed("identifier");
        }

        return Answers.Of(
            await records
                .SendFindCodeAsync(
                    browser.Asking,
                    browser.Required.Id,
                    new IdentifierId(identifier),
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false),
            Accepted);
    }

    private static async Task<IResult> VerifyAsync(
        IPersonRecords records,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordFindVerifyRequest request = await JsonBody
            .ReadAsync<RecordFindVerifyRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Code is not { Length: > 0 } code)
        {
            return Answers.Malformed("code");
        }

        return Offered(
            await records
                .VerifyFindCodeAsync(
                    browser.Asking,
                    browser.Required.Id,
                    code,
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private static async Task<IResult> AttachAsync(
        IPersonRecords records,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);

        return Attached(
            await records.AttachAsync(browser.Asking, browser.Required.Id, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> AttachByReferenceAsync(
        IPersonRecords records,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordAttachReferenceRequest request = await JsonBody
            .ReadAsync<RecordAttachReferenceRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (request.Reference?.Trim() is not { Length: > 0 and <= ReferenceLength } reference)
        {
            return Answers.Malformed("reference");
        }

        return Attached(
            await records
                .AttachByReferenceAsync(
                    browser.Asking,
                    browser.Required.Id,
                    reference,
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private static IResult Offered(Result<RecordOffer> offer) =>
        Answers.Of(
            offer,
            found => TypedResults.Json(RecordOfferView.Of(found), RecordsJson.Default.RecordOfferView));

    private static IResult Attached(Result<int> attached) =>
        Answers.Of(
            attached,
            count => TypedResults.Json(new RecordsAttachedView(count), RecordsJson.Default.RecordsAttachedView));
}
