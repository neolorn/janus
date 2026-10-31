using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// A mail server that hosts accounts in memory and honours the contract: a push whose
/// key it has applied changes nothing, a push that meets an account under the mailbox's
/// name not carrying its identifier is answered <c>integration.mailserver.conflict</c>
/// and changes nothing, and a listing answers what it holds. Its app passwords belong
/// to an account, which a call reaches by the mailbox it names: the account under the
/// mailbox's address that carries its identifier, and none other (INT-MAIL-010, D-226).
/// </summary>
internal sealed class MailServerInMemory : IMailServer
{
    private readonly Dictionary<string, Hosted> _hosted = new(StringComparer.Ordinal);

    private readonly HashSet<Guid> _applied = [];

    private readonly Dictionary<string, List<AppPassword>> _passwords = new(StringComparer.Ordinal);

    private int _generated;

    /// <summary>
    /// Every mailbox an app-password call named, in order.
    /// </summary>
    public List<MailboxId> Calls { get; } = [];

    /// <summary>
    /// Every secret the server generated, in order.
    /// </summary>
    public List<string> Secrets { get; } = [];

    /// <summary>
    /// Every push received, in order, repeats included.
    /// </summary>
    public List<MailboxPush> Received { get; } = [];

    /// <summary>
    /// Every push that changed something, in order.
    /// </summary>
    public List<MailboxPush> Applied { get; } = [];

    /// <summary>
    /// Whether every call fails, as one made while the server is unreachable does.
    /// </summary>
    public bool Unreachable { get; set; }

    /// <summary>
    /// Whether a push is applied and its answer then lost, as one whose connection
    /// drops after the server acted does.
    /// </summary>
    public bool LosesAnswers { get; set; }

    /// <summary>
    /// Whether a push ends as a call its caller gave up on does: cancelled, though
    /// nobody cancelled the pass that made it.
    /// </summary>
    public bool Abandoned { get; set; }

    /// <summary>
    /// What the test observes the moment a push arrives, before the server acts on it.
    /// </summary>
    public Action<MailboxPush>? Receiving { get; set; }

    /// <summary>
    /// What happens at the server once it has next created an app password, where a
    /// test sets it: a server that stops answering after the creation. It happens once.
    /// </summary>
    public Action? Created { get; set; }

    /// <summary>
    /// Whether the server hosts a mailbox, and whether it is enabled.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>Whether it is enabled, or nothing where it is not hosted.</returns>
    public bool? Hosts(string address) =>
        _hosted.TryGetValue(address, out Hosted? hosted) ? hosted.Enabled : null;

    /// <summary>
    /// The identifier of the library's mailbox the account at an address carries.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The identifier, or nothing where no account or none is carried.</returns>
    public MailboxId? Carried(string address) =>
        _hosted.TryGetValue(address, out Hosted? hosted) ? hosted.Mailbox : null;

    /// <summary>
    /// Changes an account on the server behind the library's back, as an operator
    /// working in the server's own console does. An account already there keeps the
    /// identifier it carries.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="enabled">Whether it is enabled, or nothing to remove it.</param>
    /// <param name="mailbox">
    /// The identifier a new account carries, or nothing for one the library did not
    /// create.
    /// </param>
    public void Set(string address, bool? enabled, MailboxId? mailbox = null)
    {
        if (enabled is bool value)
        {
            _hosted[address] = new Hosted(
                _hosted.TryGetValue(address, out Hosted? standing) ? standing.Mailbox : mailbox,
                value);
        }
        else
        {
            _ = _hosted.Remove(address);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);

        Received.Add(push);
        Receiving?.Invoke(push);

        if (Abandoned)
        {
            throw new OperationCanceledException("The push was given up.");
        }

        if (Unreachable)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        if (_hosted.TryGetValue(push.Address.Value, out Hosted? found) && found.Mailbox != push.Mailbox)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.MailServerConflict)));
        }

        if (_applied.Add(push.Key))
        {
            Applied.Add(push);
            Set(
                push.Address.Value,
                push.State switch
                {
                    MailboxState.Enabled => true,
                    MailboxState.Disabled => false,
                    _ => null,
                },
                push.Mailbox);
        }

        return ValueTask.FromResult(
            LosesAnswers ? Result.Failure(Error.From(ErrorCodes.SystemFault)) : Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Unreachable
                ? Result.Failure<IReadOnlyList<HostedMailbox>>(Error.From(ErrorCodes.SystemFault))
                : Result.Success<IReadOnlyList<HostedMailbox>>(
                    [.. _hosted.Select(pair => new HostedMailbox(pair.Value.Mailbox, Listed(pair.Key), pair.Value.Enabled))]));

    /// <summary>
    /// The app passwords the server holds for the account at an address.
    /// </summary>
    /// <param name="address">The account's address.</param>
    /// <returns>What it holds.</returns>
    public IReadOnlyList<AppPassword> AppPasswordsOf(string address) =>
        _passwords.TryGetValue(address, out List<AppPassword>? held) ? held : [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
        MailboxId mailbox,
        EmailAddress address,
        CancellationToken cancellationToken)
    {
        Calls.Add(mailbox);

        return ValueTask.FromResult(
            Holder(mailbox, address) is not string holder
                ? Result.Failure<IReadOnlyList<AppPassword>>(Error.From(ErrorCodes.SystemFault))
                : Result.Success<IReadOnlyList<AppPassword>>(
                    _passwords.TryGetValue(holder, out List<AppPassword>? held) ? [.. held] : []));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
        MailboxId mailbox,
        EmailAddress address,
        string label,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        Calls.Add(mailbox);

        if (Holder(mailbox, address) is not string holder)
        {
            return ValueTask.FromResult(Result.Failure<IssuedAppPassword>(Error.From(ErrorCodes.SystemFault)));
        }

        _generated++;

        var id = AppPasswordId.Parse("app-password-" + _generated.ToString(CultureInfo.InvariantCulture));
        string secret = "generated-secret-" + _generated.ToString(CultureInfo.InvariantCulture);

        if (!_passwords.TryGetValue(holder, out List<AppPassword>? held))
        {
            held = [];
            _passwords[holder] = held;
        }

        held.Add(new AppPassword(id, label, DateTimeOffset.UnixEpoch, expiresAt));
        Secrets.Add(secret);

        if (Created is Action created)
        {
            Created = null;
            created();
        }

        return ValueTask.FromResult(Result.Success(new IssuedAppPassword(id, secret)));
    }

    /// <inheritdoc/>
    public ValueTask<Result> RevokeAppPasswordAsync(
        MailboxId mailbox,
        EmailAddress address,
        AppPasswordId id,
        CancellationToken cancellationToken)
    {
        Calls.Add(mailbox);

        if (Holder(mailbox, address) is not string holder)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        return ValueTask.FromResult(
            _passwords.TryGetValue(holder, out List<AppPassword>? held)
            && held.RemoveAll(password => password.Id == id) > 0
                ? Result.Success()
                : Result.Failure(Error.From(ErrorCodes.CredentialNotFound)));
    }

    // An address the server holds, as a listing reads it.
    private static EmailAddress? Listed(string address) =>
        EmailAddress.TryParse(address, out EmailAddress listed) ? listed : null;

    private sealed record Hosted(MailboxId? Mailbox, bool Enabled);

    // The account a call acts on: the one under the mailbox's address that carries its
    // identifier, where the server is reached and holds one.
    private string? Holder(MailboxId mailbox, EmailAddress address) =>
        !Unreachable
        && _hosted.TryGetValue(address.Value, out Hosted? hosted)
        && hosted.Mailbox == mailbox
            ? address.Value
            : null;
}
