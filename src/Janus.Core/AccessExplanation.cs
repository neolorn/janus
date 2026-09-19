namespace Janus.Core;

/// <summary>
/// Why access was granted or refused: the grant that decided, or the absence of one.
/// </summary>
/// <param name="Outcome">What was decided.</param>
/// <param name="Permission">The permission that was asked for.</param>
/// <param name="Principal">Who it was asked for.</param>
/// <param name="Grant">The grant that decided, and nothing where none matched.</param>
/// <remarks>
/// Implements AUTHZ-GATE-004. The explanation carries codes and structured values and
/// no sentence: what a person is shown is the frontend's to write (CONV-CONTENT-001).
/// </remarks>
public sealed record AccessExplanation(
    AccessOutcome Outcome,
    Permission Permission,
    ExplainedPrincipal Principal,
    ExplainedGrant? Grant);
