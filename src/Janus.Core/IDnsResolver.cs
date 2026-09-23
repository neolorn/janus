using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What reads a name's DNS text records, which is how a domain an organization locks
/// its members' addresses to is shown to be the organization's own. The library
/// resolves no name itself: a deployment registers one of these, and until it does no
/// domain is ever verified.
/// </summary>
/// <remarks>Implements LIB-EXT-001, REG-DOM-001 and IDN-ORG-006.</remarks>
public interface IDnsResolver
{
    /// <summary>
    /// Reads the text records published at one name.
    /// </summary>
    /// <param name="name">The name, in its ASCII form.</param>
    /// <param name="cancellationToken">Abandons the lookup.</param>
    /// <returns>
    /// Each record's character-strings joined in order, an empty list where the name
    /// publishes none, or the failure where no answer could be had. A failure is never
    /// read as a record that matched.
    /// </returns>
    ValueTask<Result<IReadOnlyList<string>>> TextRecordsAsync(string name, CancellationToken cancellationToken);
}
