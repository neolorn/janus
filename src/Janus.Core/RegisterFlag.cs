namespace Janus.Core;

/// <summary>
/// One thing the generated records of processing found missing, and what it is about.
/// </summary>
/// <param name="Finding">What is missing.</param>
/// <param name="Subject">
/// What it is missing from: the purpose, the recipient or the category, and the empty
/// string where the finding is about the deployment itself.
/// </param>
/// <remarks>Implements PRIV-ROPA-001, PRIV-ROPA-002 and CONV-CONTENT-001.</remarks>
public sealed record RegisterFlag(RegisterFinding Finding, string Subject);
