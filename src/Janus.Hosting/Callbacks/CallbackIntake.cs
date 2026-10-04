using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// The steps every host callback takes before its own checks: the count per source,
/// the published ranges and the reading of its raw bytes; and the one way a refusal is
/// counted, recorded and answered.
/// </summary>
/// <remarks>
/// Implements INT-GEN-003, BFF-MACH-002 and BFF-MACH-003. The rate limit is answered
/// before anything is read or looked up; each later refusal counts towards the alert.
/// </remarks>
internal static class CallbackIntake
{
    /// <summary>
    /// Counts the callback against its source and checks the source against the
    /// published ranges, inside the transaction the caller opened.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="callback">The callback's name.</param>
    /// <param name="sources">The published ranges; empty where there are none.</param>
    /// <param name="admission">What counts callbacks.</param>
    /// <param name="work">The transaction the counts are kept in.</param>
    /// <param name="log">Where a refusal is recorded.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the callback goes on; where it does not, it has been answered.</returns>
    public static async ValueTask<bool> AdmittedAsync(
        HttpContext context,
        string callback,
        IReadOnlyCollection<IPNetwork> sources,
        CallbackAdmission admission,
        IUnitOfWork work,
        ILogger log,
        CancellationToken cancellationToken)
    {
        string source = RequestOrigin.Source(context.Request);

        Result admitted = await admission.AdmitAsync(source, cancellationToken).ConfigureAwait(false);

        if (admitted.Match(() => (Error?)null, error => error) is Error limited)
        {
            CallbackLog.Refused(log, callback, CallbackCheck.RateLimit, context.TraceIdentifier);
            await AnsweredAsync(context, limited, work, cancellationToken).ConfigureAwait(false);

            return false;
        }

        if (!Within(context.Connection.RemoteIpAddress, sources))
        {
            await RefusedAsync(context, callback, CallbackCheck.Source, admission, work, log, cancellationToken)
                .ConfigureAwait(false);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the request as it arrived, leaving the body where the host's route can
    /// read it again.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>What the provider delivered, with its raw bytes.</returns>
    public static async ValueTask<CallbackDelivery> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Request.EnableBuffering();

        using var bytes = new MemoryStream();

        await context.Request.Body.CopyToAsync(bytes, cancellationToken).ConfigureAwait(false);

        context.Request.Body.Position = 0;

        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, StringValues> header in context.Request.Headers)
        {
            headers[header.Key] = header.Value;
        }

        return new CallbackDelivery(
            context.Request.Method,
            context.Request.Query,
            headers,
            bytes.ToArray());
    }

    /// <summary>
    /// Counts, records and answers one refusal.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="callback">The callback's name.</param>
    /// <param name="check">The check that refused it.</param>
    /// <param name="admission">What counts rejections and raises the alert.</param>
    /// <param name="work">The transaction the count is kept in.</param>
    /// <param name="log">Where the refusal is recorded.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of answering it.</returns>
    public static async ValueTask RefusedAsync(
        HttpContext context,
        string callback,
        CallbackCheck check,
        CallbackAdmission admission,
        IUnitOfWork work,
        ILogger log,
        CancellationToken cancellationToken)
    {
        Error refused = await CountedAsync(context, callback, check, admission, work, log, cancellationToken)
            .ConfigureAwait(false);

        await Refusal.WriteAsync(context, refused, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts and records one refusal and ends its unit of work, leaving the answer to
    /// a caller whose provider expects a shape of its own.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="callback">The callback's name.</param>
    /// <param name="check">The check that refused it.</param>
    /// <param name="admission">What counts rejections and raises the alert.</param>
    /// <param name="work">The transaction the count is kept in.</param>
    /// <param name="log">Where the refusal is recorded.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// <c>integration.callback.rejected</c> where the refusal was counted, or the
    /// failure that kept it from being counted.
    /// </returns>
    public static async ValueTask<Error> CountedAsync(
        HttpContext context,
        string callback,
        CallbackCheck check,
        CallbackAdmission admission,
        IUnitOfWork work,
        ILogger log,
        CancellationToken cancellationToken)
    {
        CallbackLog.Refused(log, callback, check, context.TraceIdentifier);

        Result rejected = await admission
            .RejectAsync(RequestOrigin.Source(context.Request), cancellationToken)
            .ConfigureAwait(false);

        Error refused = rejected.Match(() => Error.From(ErrorCodes.CallbackRejected), error => error);

        await EndedAsync(refused, work, cancellationToken).ConfigureAwait(false);

        return refused;
    }

    /// <summary>
    /// Refuses a callback that arrived carrying the browser's session cookie, inside
    /// the transaction the caller opened: the callback is counted against its source
    /// first, as every callback is, and within the rate limit the refusal is counted as
    /// a rejection and answered as one.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="admission">What counts callbacks and rejections and raises the alert.</param>
    /// <param name="work">The transaction the counts are kept in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of answering it.</returns>
    /// <remarks>Implements BFF-MACH-001 criterion 2 and INT-GEN-003 criterion 4.</remarks>
    public static async ValueTask CookieRefusedAsync(
        HttpContext context,
        CallbackAdmission admission,
        IUnitOfWork work,
        CancellationToken cancellationToken)
    {
        string source = RequestOrigin.Source(context.Request);

        Result admitted = await admission.AdmitAsync(source, cancellationToken).ConfigureAwait(false);
        Result refused = admitted.Match(() => true, _ => false)
            ? await admission.RejectAsync(source, cancellationToken).ConfigureAwait(false)
            : admitted;

        await AnsweredAsync(
                context,
                refused.Match(() => Error.From(ErrorCodes.CallbackRejected), error => error),
                work,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask AnsweredAsync(
        HttpContext context,
        Error error,
        IUnitOfWork work,
        CancellationToken cancellationToken)
    {
        await EndedAsync(error, work, cancellationToken).ConfigureAwait(false);

        await Refusal.WriteAsync(context, error, cancellationToken).ConfigureAwait(false);
    }

    // The counts are kept whenever the callback was answered as rejected; any other
    // failure rolls the transaction back before it is answered.
    private static async ValueTask EndedAsync(Error error, IUnitOfWork work, CancellationToken cancellationToken)
    {
        if (error.Code == ErrorCodes.CallbackRejected)
        {
            (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }
        else
        {
            await work.RollbackAsync().ConfigureAwait(false);
        }
    }

    private static bool Within(IPAddress? address, IReadOnlyCollection<IPNetwork> sources)
    {
        if (sources.Count == 0)
        {
            return true;
        }

        if (address is null)
        {
            return false;
        }

        IPAddress arrived = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        foreach (IPNetwork published in sources)
        {
            if (published.Contains(arrived))
            {
                return true;
            }
        }

        return false;
    }
}
