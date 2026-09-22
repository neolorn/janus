using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One thing the generated records of processing found missing.
/// </summary>
/// <param name="Finding">What is missing.</param>
/// <param name="Subject">What it is missing from, empty where it is the deployment.</param>
/// <remarks>Implements PRIV-ROPA-001 and CONV-CONTENT-001.</remarks>
internal sealed record RegisterFlagView(RegisterFinding Finding, string Subject);
