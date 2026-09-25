using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// A mail server that hosts mailboxes in memory and honours the contract: a push whose
/// key it has applied changes nothing, and a listing answers what it holds. Its app
/// passwords belong to the person a token's <c>sub</c> names, read as a server that
/// validates tokens offline reads it; a token it cannot read is refused.
/// </summary>
internal sealed class MailServerInMemory : IMailServer
{
    private readonly Dictionary<string, bool> _hosted = new(StringComparer.Ordinal);

    private readonly HashSet<Guid> _applied = [];

    private readonly Dictionary<string, List<AppPassword>> _passwords = new(StringComparer.Ordinal);

    private int _generated;

    /// <summary>
    /// Every token an app-password call carried, in order.
    /// </summary>
    public List<string> Tokens { get; } = [];

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
    /// What the test observes the moment a push arrives, before the server acts on it.
    /// </summary>
    public Action<MailboxPush>? Receiving { get; set; }

    /// <summary>
    /// Whether the server hosts a mailbox, and whether it is enabled.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>Whether it is enabled, or nothing where it is not hosted.</returns>
    public bool? Hosts(string address) =>
        _hosted.TryGetValue(address, out bool enabled) ? enabled : null;

    /// <summary>
    /// Changes a mailbox on the server behind the library's back, as an operator
    /// working in the server's own console does.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="enabled">Whether it is enabled, or nothing to remove it.</param>
    public void Set(string address, bool? enabled)
    {
        if (enabled is bool value)
        {
            _hosted[address] = value;
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

        if (Unreachable)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        if (_applied.Add(push.Key))
        {
            Applied.Add(push);
            Set(push.Address, push.State switch
            {
                MailboxState.Enabled => true,
                MailboxState.Disabled => false,
                _ => null,
            });
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
                    [.. _hosted.Select(pair => new HostedMailbox(pair.Key, pair.Value))]));

    /// <summary>
    /// The app passwords the server holds for one person.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <returns>What it holds.</returns>
    public IReadOnlyList<AppPassword> AppPasswordsOf(SubjectId subject) =>
        _passwords.TryGetValue(subject.ToString(), out List<AppPassword>? held) ? held : [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        Tokens.Add(accessToken);

        return ValueTask.FromResult(
            Unreachable ? Result.Failure<IReadOnlyList<AppPassword>>(Error.From(ErrorCodes.SystemFault))
            : Holder(accessToken) is not string holder ? Result.Failure<IReadOnlyList<AppPassword>>(Error.From(ErrorCodes.Denied))
            : Result.Success<IReadOnlyList<AppPassword>>(
                _passwords.TryGetValue(holder, out List<AppPassword>? held) ? [.. held] : []));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
        string accessToken,
        string label,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        Tokens.Add(accessToken);

        if (Unreachable)
        {
            return ValueTask.FromResult(Result.Failure<IssuedAppPassword>(Error.From(ErrorCodes.SystemFault)));
        }

        if (Holder(accessToken) is not string holder)
        {
            return ValueTask.FromResult(Result.Failure<IssuedAppPassword>(Error.From(ErrorCodes.Denied)));
        }

        _generated++;

        string id = "app-password-" + _generated.ToString(CultureInfo.InvariantCulture);
        string secret = "generated-secret-" + _generated.ToString(CultureInfo.InvariantCulture);

        if (!_passwords.TryGetValue(holder, out List<AppPassword>? held))
        {
            held = [];
            _passwords[holder] = held;
        }

        held.Add(new AppPassword(id, label, DateTimeOffset.UnixEpoch, expiresAt));
        Secrets.Add(secret);

        return ValueTask.FromResult(Result.Success(new IssuedAppPassword(id, secret)));
    }

    /// <inheritdoc/>
    public ValueTask<Result> RevokeAppPasswordAsync(
        string accessToken,
        string id,
        CancellationToken cancellationToken)
    {
        Tokens.Add(accessToken);

        if (Unreachable)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        if (Holder(accessToken) is not string holder)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.Denied)));
        }

        return ValueTask.FromResult(
            _passwords.TryGetValue(holder, out List<AppPassword>? held)
            && held.RemoveAll(password => string.Equals(password.Id, id, StringComparison.Ordinal)) > 0
                ? Result.Success()
                : Result.Failure(Error.From(ErrorCodes.CredentialNotFound)));
    }

    // The payload of a compact token is its second segment, base64url without padding.
    private static string? Holder(string accessToken)
    {
        string[] segments = accessToken.Split('.');

        if (segments.Length is not 3)
        {
            return null;
        }

        string payload = segments[1].Replace('-', '+').Replace('_', '/');

        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        using var claims = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

        return claims.RootElement.TryGetProperty("sub", out JsonElement subject) ? subject.GetString() : null;
    }
}
