using System;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// The WebAuthn creation ceremony a registration session has open, as it is written
/// into the session's encrypted column.
/// </summary>
/// <param name="Kind">Which entry of the catalogue it creates.</param>
/// <param name="Challenge">The value the authenticator signs over.</param>
/// <param name="ExpiresAt">When it stops answering.</param>
/// <remarks>Implements REG-SESS-001 and AUTH-FACT-014.</remarks>
internal sealed record StagedCeremonyDocument(string Kind, string Challenge, DateTimeOffset ExpiresAt);
