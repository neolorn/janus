using System;
using Janus.Privacy.Exports;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where what an export contributor threw is kept: in the log, as every other fault the
/// library catches is.
/// </summary>
/// <param name="log">The host's logger.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, BFF-ERR-002 AC2, CONV-LOG-001 and CONV-LOG-003 (D-210,
/// D-211). The entry is the fault's type and frames, never its message, written while the
/// export's request runs, so the request's logging scope gives it the correlation
/// identifier the refusal answers with.
/// </remarks>
internal sealed partial class FaultEntry(ILogger<FaultEntry> log) : IFaultEntry
{
    /// <inheritdoc/>
    public void Keep(string contributor, Exception fault) => Faulted(log, contributor, FaultLog.Of(fault));

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "The export contributor {Contributor} faulted: {Fault}.")]
    private static partial void Faulted(ILogger log, string contributor, string fault);
}
