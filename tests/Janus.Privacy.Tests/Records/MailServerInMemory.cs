using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests.Records;

/// <summary>
/// A mail server the deployment registered that hosts nothing yet: every push is taken,
/// the listing is empty, and it holds no app password for anyone.
/// </summary>
internal sealed class MailServerInMemory : IMailServer
{
    /// <inheritdoc/>
    public ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success<IReadOnlyList<HostedMailbox>>([]));

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
        MailboxId mailbox,
        EmailAddress address,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success<IReadOnlyList<AppPassword>>([]));

    /// <inheritdoc/>
    public ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
        MailboxId mailbox,
        EmailAddress address,
        string label,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure<IssuedAppPassword>(Error.From(ErrorCodes.MailboxNotFound)));

    /// <inheritdoc/>
    public ValueTask<Result> RevokeAppPasswordAsync(
        MailboxId mailbox,
        EmailAddress address,
        AppPasswordId id,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.CredentialNotFound)));
}
