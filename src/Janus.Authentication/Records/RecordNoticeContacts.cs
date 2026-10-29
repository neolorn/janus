using System.Collections.Generic;

namespace Janus.Authentication.Records;

/// <summary>
/// Where a message to a person record goes: its contacts verified at a collection, or,
/// where none was, every contact it holds, in the record's language.
/// </summary>
/// <param name="Language">The record's language.</param>
/// <param name="Contacts">The notice contacts.</param>
/// <remarks>Implements REC-ACC-002.</remarks>
internal sealed record RecordNoticeContacts(string Language, IReadOnlyList<CollectedContact> Contacts);
