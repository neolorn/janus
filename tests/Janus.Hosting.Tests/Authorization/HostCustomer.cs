using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A customer of the host's, whose data about the person are the host's own plain
/// columns: no field of it is encrypted, and it names its data subject through
/// <c>Subject</c> (AUTHZ-MODEL-003, D-257).
/// </summary>
public sealed class HostCustomer
{
    /// <summary>
    /// The host's own identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The person the record is about, which is where the host reads the data subject it
    /// registers the record under.
    /// </summary>
    public SubjectId? Person { get; init; }

    /// <summary>
    /// Who referred the person, where somebody did.
    /// </summary>
    public SubjectId? Referrer { get; init; }

    /// <summary>
    /// A note of the host's about the person.
    /// </summary>
    public string Note { get; init; } = string.Empty;
}
