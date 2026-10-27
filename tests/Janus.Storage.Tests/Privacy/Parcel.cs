using System;
using Janus.Core;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// A host's own record of a parcel naming its recipient, which the library never reads:
/// only its declaration (PRIV-RIGHT-005a).
/// </summary>
public sealed class Parcel
{
    /// <summary>The recipient, whose key the parcel's encrypted fields are held under.</summary>
    public SubjectId Recipient { get; init; }

    /// <summary>The recipient's name, held as ciphertext.</summary>
    public ReadOnlyMemory<byte> RecipientName { get; init; }

    /// <summary>The recipient's telephone number, held as ciphertext.</summary>
    public ReadOnlyMemory<byte> RecipientPhone { get; init; }

    /// <summary>The recipient's signature on delivery, held as ciphertext.</summary>
    public ReadOnlyMemory<byte> Signature { get; init; }

    /// <summary>The recipient's name under the member's later name, held as ciphertext.</summary>
    public ReadOnlyMemory<byte> Addressee { get; init; }
}
