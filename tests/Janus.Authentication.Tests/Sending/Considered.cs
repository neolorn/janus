using System;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The consideration a send runs before a restricted factor goes to a number, as
/// every suite but the one about it needs it: nothing registered to answer, and the
/// record of that absence going nowhere anyone reads.
/// </summary>
internal static class Considered
{
    /// <summary>
    /// A consideration with no provider behind it.
    /// </summary>
    /// <param name="work">The one transaction an operation runs in.</param>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>The consideration.</returns>
    public static PhoneSignals Nothing(IUnitOfWork work, TimeProvider time) =>
        new(provider: null, new PhoneSignalAuditInMemory(), work, time);
}
