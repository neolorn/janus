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
        string accessToken,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success<IReadOnlyList<AppPassword>>([]));

    /// <inheritdoc/>
    public ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
        string accessToken,
        string label,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure<IssuedAppPassword>(Error.From(ErrorCodes.MailboxNotFound)));

    /// <inheritdoc/>
    public ValueTask<Result> RevokeAppPasswordAsync(
        string accessToken,
        AppPasswordId id,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.CredentialNotFound)));
}
