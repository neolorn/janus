using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's fact that a person has borrowed a binder, which the borrower role
/// is derived from: a second derivation on the binder beside the steward's.
/// </summary>
public sealed class BinderBorrower
{
    /// <summary>
    /// The binder borrowed.
    /// </summary>
    public required string BinderId { get; init; }

    /// <summary>
    /// Who borrowed it.
    /// </summary>
    public required SubjectId Borrower { get; init; }
}
