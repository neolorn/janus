using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Janus.Testing;

/// <summary>
/// A range file in the form the location and datacenter sources document, holding its
/// first line alone: <c># YYYY-MM-DD</c>, the day it is read (INT-GEN-006).
/// </summary>
internal static class DatedFile
{
    /// <summary>
    /// The file as read now.
    /// </summary>
    /// <param name="time">What gives the day.</param>
    /// <returns>The file, opened at its start.</returns>
    public static Stream Of(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        string line = "# " + time.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\n";

        return new MemoryStream(Encoding.UTF8.GetBytes(line), writable: false);
    }
}
