using System;
using System.Collections.Generic;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// One message admitted but not yet carried, as it is written into its one encrypted
/// column.
/// </summary>
/// <param name="Kind">The channel it goes out on.</param>
/// <param name="Destination">The address or the number, canonically.</param>
/// <param name="Message">Which message it is.</param>
/// <param name="Purpose">Which restrictions it answers to, which a retry is judged on.</param>
/// <param name="Source">
/// The source the send was asked for from, or nothing where no request asked for it.
/// </param>
/// <param name="Language">The language it goes out in, or nothing for a mail in every declared one.</param>
/// <param name="Subject">Whose account the destination belongs to, where it belongs to one.</param>
/// <param name="Values">The kind's places, each by name.</param>
/// <param name="Reference">
/// The correlation reference it is counted and carried under, or nothing in a row
/// written before a send carried one, whose reference is made from the row's identifier.
/// </param>
/// <param name="Languages">
/// The languages it goes out in, or nothing in a row written before a send carried them,
/// which goes out in the one language it names.
/// </param>
/// <remarks>
/// Implements D-022, AUTH-ABUSE-004 and PRIV-RIGHT-005a. Where the message goes, what
/// was asked for from, the values it carries and the reference the host names it by
/// are all held in one encrypted column: nothing queries inside an
/// undelivered message.
/// </remarks>
internal sealed record SendDeliveryDocument(
    string Kind,
    string Destination,
    string Message,
    string Purpose,
    string? Source,
    string? Language,
    Guid? Subject,
    IReadOnlyDictionary<string, string> Values,
    string? Reference = null,
    IReadOnlyList<string>? Languages = null);
