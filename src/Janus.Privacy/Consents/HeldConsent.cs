using Janus.Core;

namespace Janus.Privacy.Consents;

/// <summary>
/// One consent and whose it is.
/// </summary>
/// <param name="Subject">Whose.</param>
/// <param name="Consent">The record.</param>
/// <remarks>
/// Implements PRIV-CONS-007. Supersession works across subjects, so what it reads is
/// records with their holders rather than one subject's list.
/// </remarks>
internal sealed record HeldConsent(SubjectId Subject, ConsentRecord Consent);
