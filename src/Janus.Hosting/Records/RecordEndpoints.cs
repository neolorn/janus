using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Records;

/// <summary>
/// The person-record endpoints of chapter 09 section 8a: what staff do to a record of
/// someone who holds no account.
/// </summary>
/// <remarks>
/// Implements REC-COL-003, REC-ADM-001 and LIB-API-005. Each maps one operation of
/// <see cref="IPersonRecords"/>.
/// </remarks>
internal static class RecordEndpoints
{
    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapRecords(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = SessionRequired.On(endpoints.MapPost("/admin/records", EnterAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.Restricted,
                    ErrorCodes.RequestInvalid, ErrorCodes.IdentifierInvalid, ErrorCodes.ProfileInvalid,
                    ErrorCodes.ProfileUnderage, ErrorCodes.PurposeNoConsent, ErrorCodes.NoticeUnpublished))
            .Accepts<RecordEntryBody>(MediaTypeNames.Application.Json)
            .Produces<EnteredRecordView>(StatusCodes.Status201Created);

        _ = SessionRequired.On(endpoints.MapPost("/admin/records/find", FindAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.RequestMalformed, ErrorCodes.Denied))
            .Accepts<RecordFindBody>(MediaTypeNames.Application.Json)
            .Produces<RecordSubjectsView>(StatusCodes.Status200OK);

        return endpoints;
    }

    // REC-COL-003: what the person gave staff, the record's contacts unverified.
    private static async Task<IResult> EnterAsync(
        IPersonRecords records,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordEntryBody body = await JsonBody
            .ReadAsync<RecordEntryBody>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (body.Organization is not Guid organization)
        {
            return Answers.Malformed("organization");
        }

        if (body.Language is not string language)
        {
            return Answers.Malformed("language");
        }

        return Answers.Of(
            await records
                .EnterAsync(
                    browser.Asking,
                    new PersonRecordEntry(
                        new OrganizationId(organization),
                        language,
                        body.Name,
                        body.Email,
                        body.Phone,
                        body.Consents ?? [],
                        body.DateOfBirth,
                        body.Reference),
                    cancellationToken)
                .ConfigureAwait(false),
            entered => TypedResults.Json(
                EnteredRecordView.Of(entered),
                RecordsJson.Default.EnteredRecordView,
                contentType: null,
                StatusCodes.Status201Created));
    }

    // REC-ADM-001: the records the organization holds that hold a contact or a
    // reference.
    private static async Task<IResult> FindAsync(
        IPersonRecords records,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        RecordFindBody body = await JsonBody
            .ReadAsync<RecordFindBody>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (body.Organization is not Guid named)
        {
            return Answers.Malformed("organization");
        }

        var organization = new OrganizationId(named);

        if (body.Reference is string reference)
        {
            return body is { Kind: null, Value: null }
                ? Found(await records
                    .FindAsync(browser.Asking, organization, reference, cancellationToken)
                    .ConfigureAwait(false))
                : Answers.Malformed("reference");
        }

        if (body.Kind is not IdentifierKind kind)
        {
            return Answers.Malformed("kind");
        }

        if (body.Value is not string value)
        {
            return Answers.Malformed("value");
        }

        return Found(await records
            .FindAsync(browser.Asking, organization, kind, value, cancellationToken)
            .ConfigureAwait(false));
    }

    private static IResult Found(Result<IReadOnlyList<SubjectId>> found) =>
        Answers.Of(
            found,
            subjects => TypedResults.Json(
                RecordSubjectsView.Of(subjects),
                RecordsJson.Default.RecordSubjectsView,
                contentType: null,
                StatusCodes.Status200OK));
}
