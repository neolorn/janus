using System.Collections.Generic;
using Janus.Core.Configuration;

namespace Janus.Core;

/// <summary>
/// One recipient of personal data, as the deployment declares it: the six columns the
/// records of processing report for it.
/// </summary>
/// <param name="Name">What the recipient is called.</param>
/// <param name="Characterisation">What it is in law.</param>
/// <param name="DataReceived">The categories of data it receives.</param>
/// <param name="Location">
/// Where it is, or nothing where it is wherever the deployment is hosted, which is
/// what a provider following the hosting is.
/// </param>
/// <param name="AgreementReference">
/// The data protection agreement, where one is signed. A processor without one is
/// flagged in the generated records rather than left out of them.
/// </param>
/// <param name="Callback">Whether it calls back into the deployment.</param>
/// <remarks>
/// Implements PRIV-ROPA-001, PRIV-ROPA-002 and chapter 05 section 6. The register is
/// a rendering of what was declared, so a recipient that is not declared cannot be
/// sent anything and a recipient that is appears without anyone maintaining a list.
/// </remarks>
public sealed record RecipientDeclaration(
    string Name,
    RecipientCharacterisation Characterisation,
    IReadOnlyList<string> DataReceived,
    HostingLocation? Location,
    string? AgreementReference,
    bool Callback);
