using Janus.Core;

namespace Janus.Privacy.Consents;

/// <summary>
/// One consent a statement ended: whose it was and for which purpose.
/// </summary>
/// <param name="Subject">Whose.</param>
/// <param name="Purpose">The purpose it was given for.</param>
/// <remarks>
/// Implements PRIV-CONS-007. The statement that stamps the consents a start finds
/// against another document answers which it stamped, so each is announced once.
/// </remarks>
internal sealed record EndedConsent(SubjectId Subject, string Purpose);
