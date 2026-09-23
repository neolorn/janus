using System;
using System.Collections.Generic;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// One message undertaken but not yet carried, as it is written into its one
/// encrypted column.
/// </summary>
/// <param name="Kind">The channel it goes out on.</param>
/// <param name="Destination">The address or the number, canonically.</param>
/// <param name="Message">Which message it is.</param>
/// <param name="Purpose">Which restrictions it answered to.</param>
/// <param name="Source">The address the send was asked for from.</param>
/// <param name="Language">The language it goes out in.</param>
/// <param name="Subject">Whose account the destination belongs to, where it belongs to one.</param>
/// <param name="Values">What the library puts in the template's places.</param>
/// <remarks>
/// Implements D-022 and PRIV-RIGHT-005a. Where the message goes, what was asked for
/// from and what the template is given are all personal, so the whole of it is one
/// encrypted column: nothing queries inside an undelivered message.
/// </remarks>
internal sealed record SendDeliveryDocument(
    string Kind,
    string Destination,
    string Message,
    string Purpose,
    string Source,
    string Language,
    Guid? Subject,
    IReadOnlyDictionary<string, string> Values);
