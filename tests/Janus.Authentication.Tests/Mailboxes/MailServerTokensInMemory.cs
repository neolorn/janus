using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Mailboxes;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// The tokens the provider issues to the mail server's client, held in memory: a
/// compact token whose <c>sub</c> is the session's subject, issued only from a session
/// that is live and is the subject's.
/// </summary>
/// <param name="sessions">Where the session the token stands on is read.</param>
/// <param name="time">The clock the test runs on.</param>
internal sealed class MailServerTokensInMemory(ISessionStore sessions, TimeProvider time) : IMailServerTokens
{
    /// <summary>
    /// Every token issued, in order.
    /// </summary>
    public List<string> Issued { get; } = [];

    /// <inheritdoc/>
    public async ValueTask<Result<string>> IssueAsync(
        SubjectId subject,
        SessionId session,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        if (await sessions.FindAsync(session, cancellationToken) is not Session live
            || live.EndedAt is not null
            || now >= live.AbsoluteExpiry
            || now >= live.IdleExpiry)
        {
            return Result.Failure<string>(Error.From(ErrorCodes.SessionExpired));
        }

        if (live.Subject != subject)
        {
            return Result.Failure<string>(Error.From(ErrorCodes.Denied));
        }

        string token = Segment("{\"alg\":\"none\"}") + "." + Segment("{\"sub\":\"" + subject + "\"}") + ".";

        Issued.Add(token);

        return Result.Success(token);
    }

    private static string Segment(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
