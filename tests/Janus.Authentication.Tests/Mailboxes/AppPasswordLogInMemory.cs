using System.Collections.Generic;
using Janus.Authentication.Mailboxes;
using Janus.Core;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// Where an app password left at the mail server is written down, holding the entries
/// so a test can read them.
/// </summary>
internal sealed class AppPasswordLogInMemory : IAppPasswordLog
{
    /// <summary>
    /// Every revocation recorded as failed, in order.
    /// </summary>
    public List<(SubjectId Holder, ErrorCode Failure)> Unrevoked { get; } = [];

    /// <inheritdoc/>
    public void RevocationFailed(SubjectId holder, ErrorCode failure) => Unrevoked.Add((holder, failure));
}
