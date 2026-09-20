using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One provider or other party that receives personal data, as the generated records
/// of processing list it.
/// </summary>
/// <param name="Name">What it is called.</param>
/// <param name="Characterisation">What it is in law.</param>
/// <param name="DataReceived">The categories it receives.</param>
/// <param name="Location">Whether it receives them inside the jurisdiction.</param>
/// <param name="AgreementReference">
/// The agreement under which it processes, absent where the deployment has not named
/// one, which the records flag.
/// </param>
/// <param name="Callback">Whether it calls back into the deployment.</param>
/// <remarks>Implements PRIV-ROPA-002, INT-GEN-004, LIB-HOST-001, chapter 5 section 8.</remarks>
public sealed record Recipient(
    string Name,
    RecipientCharacterisation Characterisation,
    IReadOnlyList<string> DataReceived,
    RecipientLocation Location,
    string? AgreementReference,
    bool Callback);
