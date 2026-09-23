namespace Janus.Core;

/// <summary>
/// What this application calls itself at the provider when it establishes its own
/// session from the one the authentication application holds.
/// </summary>
/// <param name="ClientId">The identifier the deployment registered it under.</param>
/// <remarks>
/// Implements BFF-SESS-006, BFF-OWN-001 and LIB-HOST-001. Every application of a
/// deployment is a confidential client of the one provider, and which of them this
/// process is is the one thing the library cannot work out for itself: there is no
/// default, and a deployment that declares none does not start. The secret the client
/// presents is not here, because a declaration is configuration and a secret is never
/// in configuration (OPS-SEC-001).
/// </remarks>
public sealed record SignOnClient(string ClientId);
