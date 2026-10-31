using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>sends</c> row: one message admitted, the send it is of, the keys it counted
/// against, what its send counted once, and when the last of those counts stops
/// deciding anything.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004, INT-SMS-005 and chapter 10 section 5.16a. The correlation
/// reference is held by its hash, so a table dump yields nothing a forged callback could
/// present. What a send counts once against the restrictions marked <c>resend</c> is
/// written the same on the row of every message of the send, and on none once released.
/// </remarks>
internal sealed class SendRecord
{
    /// <summary>The <c>reference</c> column, which is this table key.</summary>
    public byte[] Reference { get; set; } = [];

    /// <summary>
    /// The <c>send</c> column: the hash of the reference of the first message of the
    /// send this message is of, its own where it is the first or the only one.
    /// </summary>
    public byte[] Send { get; set; } = [];

    /// <summary>
    /// The <c>admitted</c> column: whether the message holds the count of an admission,
    /// which one set aside to be judged again does not.
    /// </summary>
    public bool Admitted { get; set; }

    /// <summary>
    /// The <c>released</c> column: whether the message failed for good, its counts
    /// taken back, which a report of it still names until its send settles (D-227).
    /// </summary>
    public bool Released { get; set; }

    /// <summary>The <c>counted</c> column.</summary>
    public byte[][] Counted { get; set; } = [];

    /// <summary>
    /// The <c>spent</c> column: the hashed keys whose granted credit the send consumed,
    /// which a release gives back.
    /// </summary>
    public byte[][] Spent { get; set; } = [];

    /// <summary>
    /// The <c>spent_versions</c> column: for each key of <c>spent</c>, the version of the
    /// fingerprint key its grant stood under.
    /// </summary>
    public int[] SpentVersions { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// keys counted are hashed under.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>The <c>sent_at</c> column.</summary>
    public DateTimeOffset SentAt { get; set; }

    /// <summary>The <c>settles_at</c> column.</summary>
    public DateTimeOffset SettlesAt { get; set; }

    /// <summary>
    /// The <c>resend</c> column: the hashed keys of the restrictions marked
    /// <c>resend</c> the send counts once against, and none once that count is released.
    /// </summary>
    public byte[][] Resend { get; set; } = [];

    /// <summary>
    /// The <c>resend_at</c> column: the instant the send counts once at, and nothing
    /// where it holds no such count.
    /// </summary>
    public DateTimeOffset? ResendAt { get; set; }

    /// <summary>
    /// The <c>second_factor</c> column: whether the send's purpose is
    /// <c>secondfactor</c>, which lets a report that a text of it was delivered release
    /// what it counts once.
    /// </summary>
    public bool SecondFactor { get; set; }

    /// <summary>
    /// The <c>answer</c> column: the hash that names the code or the link the send
    /// carries, and nothing where it carries none or none was named.
    /// </summary>
    public byte[]? Answer { get; set; }
}
