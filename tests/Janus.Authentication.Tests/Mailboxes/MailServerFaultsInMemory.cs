using System;
using System.Collections.Generic;
using Janus.Authentication.Mailboxes;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// Where the mail server's faults are kept, so a test can read back what the publisher
/// kept.
/// </summary>
internal sealed class MailServerFaultsInMemory : IMailServerFaults
{
    private readonly List<Exception> _kept = [];

    /// <summary>
    /// Every fault kept, in the order it was.
    /// </summary>
    public IReadOnlyList<Exception> Kept => _kept;

    /// <inheritdoc/>
    public void Keep(Exception fault) => _kept.Add(fault);
}
