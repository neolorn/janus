using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Why access was granted or refused, as the gate explains it.
/// </summary>
/// <param name="Outcome">What was decided.</param>
/// <param name="Permission">The permission that was asked for.</param>
/// <param name="Principal">Who it was asked for.</param>
/// <param name="Grant">The grant that decided, or nothing where none matched.</param>
/// <remarks>Implements AUTHZ-GATE-004 in the shape D-153 fixes.</remarks>
internal sealed record ExplanationView(
    AccessOutcome Outcome,
    string Permission,
    ExplainedPrincipalView Principal,
    ExplainedGrantView? Grant)
{
    /// <summary>
    /// The view of an explanation.
    /// </summary>
    /// <param name="explanation">The explanation.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The explanation is absent.</exception>
    public static ExplanationView Of(AccessExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        return new ExplanationView(
            explanation.Outcome,
            explanation.Permission.ToString(),
            new ExplainedPrincipalView(
                explanation.Principal.Acting?.Value,
                explanation.Principal.Effective?.Value),
            explanation.Grant is ExplainedGrant grant ? ExplainedGrantView.Of(grant) : null);
    }
}
