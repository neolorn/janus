using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Janus.Storage;

namespace Janus.Hosting;

/// <summary>
/// What is kept in the log of a fault the library caught: the pipeline's, a background
/// job's and a restore test step's alike.
/// </summary>
/// <remarks>
/// Implements BFF-ERR-002 AC2. A fault is kept by its full type name and its stack
/// frames, and by those of each inner fault, and never by a message or by
/// <see cref="Exception.ToString"/>, which carries the messages, because a message can
/// carry a value (CONV-LOG-003). A fault that carries a code is kept by that code and
/// its structured details as well, which name what broke and hold no value of a person
/// or a secret (CONV-CODE-007, OPS-SEC-003 AC3).
/// </remarks>
internal static class FaultLog
{
    /// <summary>
    /// The entry a fault is kept in the log as.
    /// </summary>
    /// <param name="fault">What was thrown.</param>
    /// <returns>
    /// The type and frames of the fault and of each inner fault, outermost first, each
    /// with the code and details it carries where it carries any.
    /// </returns>
    /// <exception cref="ArgumentNullException">The fault is absent.</exception>
    public static string Of(Exception fault)
    {
        ArgumentNullException.ThrowIfNull(fault);

        var entry = new StringBuilder();

        for (Exception? at = fault; at is not null; at = at.InnerException)
        {
            if (entry.Length > 0)
            {
                entry.AppendLine().Append("inner ");
            }

            entry.Append(at.GetType().FullName);

            if (at is CodedFault coded)
            {
                entry.Append(' ').Append(coded.Failure.Code);

                foreach ((string name, JsonElement value) in coded.Failure.Details.OrderBy(detail => detail.Key, StringComparer.Ordinal))
                {
                    entry.Append(' ').Append(name).Append('=').Append(value.GetRawText());
                }
            }

            if (at.StackTrace is string frames)
            {
                entry.AppendLine().Append(frames);
            }
        }

        return entry.ToString();
    }
}
