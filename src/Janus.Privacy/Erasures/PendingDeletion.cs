using System;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// An account standing in its deletion grace window: whose it is, what began it, and
/// when the window started running.
/// </summary>
/// <param name="Subject">Whose account it is.</param>
/// <param name="By">What began the deletion.</param>
/// <param name="Since">When the window began.</param>
/// <remarks>
/// Implements IDN-LIFE-014 and IDN-LIFE-003. What began the deletion decides the
/// reason the erasure carries to the subscribers, so the sweep reads it here rather
/// than assuming every window was the subject's own doing.
/// </remarks>
internal sealed record PendingDeletion(SubjectId Subject, DeletionOrigin By, DateTimeOffset Since);
