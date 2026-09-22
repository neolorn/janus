using System.Collections.Generic;
using Janus.Core.Configuration;

namespace Janus.Core;

/// <summary>
/// One recipient row of the generated records of processing: what was declared about
/// it, resolved against where the deployment is hosted.
/// </summary>
/// <param name="Name">What the recipient is called.</param>
/// <param name="Characterisation">What it is in law.</param>
/// <param name="DataReceived">The categories of data it receives.</param>
/// <param name="Location">Where it is, the deployment's own where it follows it.</param>
/// <param name="AgreementReference">The data protection agreement, where one exists.</param>
/// <param name="CrossBorderBasis">
/// What the transfer to it rests on, where it is outside and the data therefore
/// crosses a border. It is the deployment's declared basis and never a consent
/// (PRIV-CONS-010).
/// </param>
/// <param name="Callback">Whether it calls back into the deployment.</param>
/// <remarks>Implements PRIV-ROPA-002 and PRIV-ROPA-003.</remarks>
public sealed record RecipientRecord(
    string Name,
    RecipientCharacterisation Characterisation,
    IReadOnlyList<string> DataReceived,
    HostingLocation Location,
    string? AgreementReference,
    string? CrossBorderBasis,
    bool Callback);
