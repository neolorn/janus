namespace Janus.Core;

/// <summary>
/// Which client of the provider the mail server is: the first-party client the library
/// obtains the signed-in person's token through when it manages their app passwords.
/// </summary>
/// <param name="ClientId">The identifier the deployment registered it under.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 AC4, INT-MAIL-010 and LIB-HOST-001. Required where the
/// deployment registers a mail server and not otherwise: the registry may hold more
/// than one protocol client, and which of them the mail server trusts is the
/// deployment's to say. The client's secret is not here, because the library issues
/// the token itself and presents nothing on the client's behalf.
/// </remarks>
public sealed record MailServerClient(string ClientId);
