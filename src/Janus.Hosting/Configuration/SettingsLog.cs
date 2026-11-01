using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Configuration;

/// <summary>
/// What the start records about the settings it read.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5a, CONV-LOG-001 and CONV-LOG-003 (D-235, D-238). A
/// policy is named by its key and a gate by its action's name, so no value a policy
/// holds reaches the log.
/// </remarks>
internal static partial class SettingsLog
{
    /// <summary>
    /// A stored policy or override names step-up actions the library has retired, whose
    /// gates every read passes over.
    /// </summary>
    /// <param name="log">The logger.</param>
    /// <param name="policy">The policy's key.</param>
    /// <param name="retired">The retired actions it names, written separated by commas.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "The stored policy {Policy} names the retired step-up actions {Retired}, whose gates are passed over.")]
    public static partial void RetiredGatesPassedOver(ILogger log, string policy, IReadOnlyList<string> retired);
}
