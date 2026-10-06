using System.Globalization;
using Janus.Privacy.SubjectKeys;

namespace Janus.Storage;

/// <summary>
/// The check a column that can hold a subject identifier carries against the max UUID of
/// RFC 9562, the deployment's data key's row of the subject-key table.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005a (D-174). Whatever the table's key and whether or not the
/// column has a foreign key, the database refuses the identifier no subject is issued, so
/// no row can name the deployment's key as a subject's. The nil subject, which means no
/// subject, stays admitted wherever the column admits it, and an absent value passes.
/// </remarks>
internal static class MaxUuid
{
    /// <summary>
    /// The check expression refusing the max UUID.
    /// </summary>
    /// <param name="column">The column it constrains.</param>
    /// <returns>The expression.</returns>
    public static string Refused(string column) =>
        string.Create(CultureInfo.InvariantCulture, $"{column} <> '{SubjectKeyId.Deployment.Value:D}'");
}
