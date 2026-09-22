using System.Collections.Generic;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One recipient row of the generated records of processing.
/// </summary>
/// <param name="Name">What the recipient is called.</param>
/// <param name="Characterisation">What it is in law.</param>
/// <param name="DataReceived">The categories of data it receives.</param>
/// <param name="Location">Where it is.</param>
/// <param name="AgreementReference">The data protection agreement, where one exists.</param>
/// <param name="CrossBorderBasis">What a transfer to it rests on, where it is outside.</param>
/// <param name="Callback">Whether it calls back into the deployment.</param>
/// <remarks>Implements PRIV-ROPA-002 and PRIV-ROPA-003.</remarks>
internal sealed record RecipientRecordView(
    string Name,
    RecipientCharacterisation Characterisation,
    IReadOnlyList<string> DataReceived,
    HostingLocation Location,
    string? AgreementReference,
    string? CrossBorderBasis,
    bool Callback);
