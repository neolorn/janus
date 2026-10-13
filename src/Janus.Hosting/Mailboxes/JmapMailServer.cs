using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Mailboxes;

/// <summary>
/// The mail server the library ships an adapter for, met through its JMAP management
/// interface and nothing else.
/// </summary>
/// <param name="channel">Where the connection to the mail server comes from.</param>
/// <param name="ring">The key ring the mail server's management key is borrowed from.</param>
/// <remarks>
/// Implements INT-MAIL-001, INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007, INT-MAIL-010,
/// D-166, D-176 and D-177. Every call is one JMAP request (RFC 8620) posted to
/// <c>&lt;endpoint&gt;/jmap</c>. A status other than 2xx, a timeout, an answer that does
/// not read, a method error and any entry the server reports as not created, not updated
/// or not destroyed is a failure; nothing is read as success by default. A mailbox is an
/// account named by its address's local part in the domain of its address, and carries
/// the library's identifier of the mailbox as its description; an account under that
/// name that does not carry it is never adopted, changed or destroyed. A push changes the
/// <c>authenticate</c> permission alone. The app-password calls carry the person's token
/// and never the management key. Registered as its own type, never as
/// <see cref="IMailServer"/>: the start chooses it (CONV-DESIGN-007).
/// </remarks>
internal sealed class JmapMailServer(IHttpClientFactory channel, IKeyRing ring) : IMailServer
{
    /// <summary>
    /// The client the mail server is called with, which a host configures the way it
    /// configures every other client of the framework's factory.
    /// </summary>
    public const string Channel = "identity-mailserver";

    private const string Authenticate = "authenticate";

    private Uri? _endpoint;

    /// <inheritdoc/>
    public async ValueTask<Result> ProvisionAsync(MailboxPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);

        string address = push.Address.Value;
        int at = address.LastIndexOf('@');
        string local = address[..at];
        string domain = address[(at + 1)..];
        Error? failure = null;

        string token = Managed().Match(value => value, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        string domainId = (await DomainAsync(token, domain, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        Account? found = (await AccountAsync(token, local, domainId, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Account?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (found is not null)
        {
            return await AppliedAsync(token, push, found, cancellationToken).ConfigureAwait(false);
        }

        // INT-MAIL-001, D-177: a removal that finds no account under the mailbox's name
        // is done.
        if (push.State is MailboxState.Removed)
        {
            return Result.Success();
        }

        bool created = (await CreatedAsync(token, push, local, domainId, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        if (failure is not null || created)
        {
            return failure is null ? Result.Success() : Result.Failure(failure);
        }

        // D-166 215: a create the server refused because the name exists leads to the
        // account under it, which is the mailbox's only where it carries its identifier.
        Account? existing = (await AccountAsync(token, local, domainId, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Account?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return existing is null
            ? Result.Failure(Fault())
            : await AppliedAsync(token, push, existing, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<HostedMailbox>>> MailboxesAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        string token = Managed().Match(value => value, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<HostedMailbox>>(failure);
        }

        IReadOnlyList<JsonElement> accounts = (await ListedAsync(
                    token,
                    "x:Account",
                    new JsonObject(),
                    ["emailAddress", "description", "permissions"],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<HostedMailbox>>(failure);
        }

        var hosted = new List<HostedMailbox>(accounts.Count);

        foreach (JsonElement account in accounts)
        {
            // D-166 215 point 5: users only; an account the server says is of another
            // kind is no mailbox.
            if (account.TryGetProperty("@type", out JsonElement kind)
                && !string.Equals(kind.GetString(), "User", StringComparison.Ordinal))
            {
                continue;
            }

            if (Permitted(account) is not bool enabled)
            {
                return Result.Failure<IReadOnlyList<HostedMailbox>>(Fault());
            }

            // INT-MAIL-007 AC9: an address that does not read, or an account holding
            // none, is listed with no address and never fails the listing.
            hosted.Add(new HostedMailbox(
                Carried(account),
                Text(account, "emailAddress") is string address
                && EmailAddress.TryParse(address, out EmailAddress listed)
                    ? listed
                    : null,
                enabled));
        }

        return Result.Success<IReadOnlyList<HostedMailbox>>(hosted);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<AppPassword>>> AppPasswordsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        Error? failure = null;

        IReadOnlyList<JsonElement> answered = (await CalledAsync(
                    accessToken,
                    [
                        new Call("x:AppPassword/get", new JsonObject
                        {
                            ["ids"] = null,
                            ["properties"] = new JsonArray("description", "createdAt", "expiresAt"),
                        }),
                    ],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<AppPassword>>(failure);
        }

        if (!answered[0].TryGetProperty("list", out JsonElement list) || list.ValueKind is not JsonValueKind.Array)
        {
            return Result.Failure<IReadOnlyList<AppPassword>>(Fault());
        }

        var held = new List<AppPassword>(list.GetArrayLength());

        foreach (JsonElement entry in list.EnumerateArray())
        {
            // RFC 8620 section 1.2, INT-MAIL-001: an identifier outside the form of an Id
            // is an answer that does not read, as an absent one is.
            if (!AppPasswordId.TryParse(Text(entry, "id"), out AppPasswordId id)
                || Text(entry, "description") is not string label
                || Instant(entry, "createdAt") is not DateTimeOffset createdAt
                || !Expiry(entry, out DateTimeOffset? expiresAt))
            {
                return Result.Failure<IReadOnlyList<AppPassword>>(Fault());
            }

            held.Add(new AppPassword(id, label, createdAt, expiresAt));
        }

        return Result.Success<IReadOnlyList<AppPassword>>(held);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IssuedAppPassword>> CreateAppPasswordAsync(
        string accessToken,
        string label,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);
        ArgumentNullException.ThrowIfNull(label);

        Error? failure = null;

        IReadOnlyList<JsonElement> answered = (await CalledAsync(
                    accessToken,
                    [
                        new Call("x:AppPassword/set", new JsonObject
                        {
                            ["create"] = new JsonObject
                            {
                                ["password"] = new JsonObject
                                {
                                    ["description"] = label,
                                    ["expiresAt"] = expiresAt is DateTimeOffset expiry ? Written(expiry) : null,
                                    ["permissions"] = new JsonObject { ["@type"] = "Inherit" },
                                    ["allowedIps"] = new JsonObject(),
                                },
                            },
                        }),
                    ],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IssuedAppPassword>(failure);
        }

        // RFC 8620 section 5.3: a created object carries the server-set properties the
        // client did not send, the secret and the identifier among them.
        return answered[0].TryGetProperty("created", out JsonElement created)
            && created.ValueKind is JsonValueKind.Object
            && created.TryGetProperty("password", out JsonElement issued)
            && AppPasswordId.TryParse(Text(issued, "id"), out AppPasswordId id)
            && Text(issued, "secret") is string secret
            && secret.Length is not 0
                ? Result.Success(new IssuedAppPassword(id, secret))
                : Result.Failure<IssuedAppPassword>(Fault());
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RevokeAppPasswordAsync(
        string accessToken,
        AppPasswordId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);

        string named = id.ToString();
        Error? failure = null;

        IReadOnlyList<JsonElement> answered = (await CalledAsync(
                    accessToken,
                    [new Call("x:AppPassword/set", new JsonObject { ["destroy"] = new JsonArray(named) })],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (Destroyed(answered[0], named))
        {
            return Result.Success();
        }

        // INT-MAIL-010: an app password the server holds no more, or never held, for the
        // person is not found.
        return Result.Failure(Error.From(NotFound(answered[0], "notDestroyed", named)
            ? ErrorCodes.CredentialNotFound
            : ErrorCodes.SystemFault));
    }

    /// <summary>
    /// Sets where the mail server is reached, once, as the start chooses the adapter.
    /// </summary>
    /// <param name="endpoint">The value of <c>integration.mailserver.endpoint</c>, an absolute https address.</param>
    /// <exception cref="InvalidOperationException">The endpoint is set already.</exception>
    internal void Reach(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var reached = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/jmap");

        if (Interlocked.CompareExchange(ref _endpoint, reached, comparand: null) is not null)
        {
            throw new InvalidOperationException("The mail server's endpoint is set once, at the start.");
        }
    }

    private static Error Fault() => Error.From(ErrorCodes.SystemFault);

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind is JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    // RFC 8620 section 1.4: a UTCDate is an RFC 3339 date-time in UTC.
    private static DateTimeOffset? Instant(JsonElement element, string name) =>
        Text(element, name) is string written
        && DateTimeOffset.TryParse(
            written,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset instant)
            ? instant
            : null;

    private static bool Expiry(JsonElement element, out DateTimeOffset? expiresAt)
    {
        expiresAt = null;

        if (!element.TryGetProperty("expiresAt", out JsonElement value) || value.ValueKind is JsonValueKind.Null)
        {
            return true;
        }

        expiresAt = Instant(element, "expiresAt");

        return expiresAt is not null;
    }

    private static string Written(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // INT-MAIL-001: the identifier an account's description carries, where it carries one.
    private static MailboxId? Carried(JsonElement account) =>
        Text(account, "description") is string description
        && Guid.TryParseExact(description, "D", out Guid carried)
            ? new MailboxId(carried)
            : null;

    // Whether a set, encoded as an object mapping each member to true, holds a member.
    private static bool Holds(JsonElement permissions, string set) =>
        permissions.TryGetProperty(set, out JsonElement members)
        && members.ValueKind is JsonValueKind.Object
        && members.TryGetProperty(Authenticate, out JsonElement member)
        && member.ValueKind is JsonValueKind.True;

    private static string? Kind(JsonElement account) =>
        account.TryGetProperty("permissions", out JsonElement permissions)
            ? Text(permissions, "@type")
            : null;

    // INT-MAIL-001, D-176: enabled exactly where authenticate is not disabled and, where
    // the account's own permissions replace the inherited ones, is enabled.
    private static bool? Permitted(JsonElement account)
    {
        if (!account.TryGetProperty("permissions", out JsonElement permissions))
        {
            return null;
        }

        return Kind(account) switch
        {
            "Inherit" => true,
            "Merge" => !Holds(permissions, "disabledPermissions"),
            "Replace" => !Holds(permissions, "disabledPermissions") && Holds(permissions, "enabledPermissions"),
            _ => null,
        };
    }

    // D-176: the patch that gives the account the state a push carries, changing the
    // authenticate permission alone. RFC 8620 section 5.3 lets a patch name a member
    // of a set only where the set exists, so a set that is absent is written whole.
    private static JsonObject? Patch(JsonElement account, MailboxState state)
    {
        if (!account.TryGetProperty("permissions", out JsonElement permissions))
        {
            return null;
        }

        string? kind = Kind(account);

        if (kind is not ("Inherit" or "Merge" or "Replace"))
        {
            return null;
        }

        var patch = new JsonObject();

        if (state is MailboxState.Disabled)
        {
            if (kind is "Inherit")
            {
                patch["permissions"] = Merged();
            }
            else if (!Holds(permissions, "disabledPermissions"))
            {
                Member(patch, permissions, "disabledPermissions", true);
            }

            return patch;
        }

        if (Holds(permissions, "disabledPermissions"))
        {
            patch["permissions/disabledPermissions/" + Authenticate] = null;
        }

        if (kind is "Replace" && !Holds(permissions, "enabledPermissions"))
        {
            Member(patch, permissions, "enabledPermissions", true);
        }

        return patch;
    }

    private static void Member(JsonObject patch, JsonElement permissions, string set, bool value)
    {
        if (permissions.TryGetProperty(set, out JsonElement members) && members.ValueKind is JsonValueKind.Object)
        {
            patch["permissions/" + set + "/" + Authenticate] = value;
        }
        else
        {
            patch["permissions/" + set] = new JsonObject { [Authenticate] = value };
        }
    }

    // D-166 215 point 4: the permissions a mailbox is created disabled with, and the
    // ones an inheriting account is given when it is disabled.
    private static JsonObject Merged() =>
        new()
        {
            ["@type"] = "Merge",
            ["enabledPermissions"] = new JsonObject(),
            ["disabledPermissions"] = new JsonObject { [Authenticate] = true },
        };

    private static bool Listed(JsonElement answer, string outcome, string key) =>
        answer.TryGetProperty(outcome, out JsonElement entries)
        && entries.ValueKind is JsonValueKind.Object
        && entries.TryGetProperty(key, out _);

    private static bool Destroyed(JsonElement answer, string id)
    {
        if (!answer.TryGetProperty("destroyed", out JsonElement destroyed) || destroyed.ValueKind is not JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement entry in destroyed.EnumerateArray())
        {
            if (entry.ValueKind is JsonValueKind.String && string.Equals(entry.GetString(), id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NotFound(JsonElement answer, string outcome, string key) =>
        answer.TryGetProperty(outcome, out JsonElement entries)
        && entries.ValueKind is JsonValueKind.Object
        && entries.TryGetProperty(key, out JsonElement refusal)
        && string.Equals(Text(refusal, "type"), "notFound", StringComparison.Ordinal);

    private Result<string> Managed() =>
        ring.BorrowMailServerSecret(secret => Encoding.UTF8.GetString(secret.Span));

    private async ValueTask<Result> AppliedAsync(
        string token,
        MailboxPush push,
        Account account,
        CancellationToken cancellationToken)
    {
        // INT-MAIL-001 AC4, D-177: an account under the mailbox's name that does not
        // carry its identifier is not the library's, and nothing is done to it.
        if (Carried(account.Element) != push.Mailbox)
        {
            return Result.Failure(Error.From(ErrorCodes.MailServerConflict));
        }

        JsonObject arguments;

        if (push.State is MailboxState.Removed)
        {
            arguments = new JsonObject { ["destroy"] = new JsonArray(account.Id) };
        }
        else if (Patch(account.Element, push.State) is not JsonObject patch)
        {
            return Result.Failure(Fault());
        }
        else if (patch.Count is 0)
        {
            return Result.Success();
        }
        else
        {
            arguments = new JsonObject { ["update"] = new JsonObject { [account.Id] = patch } };
        }

        Error? failure = null;

        IReadOnlyList<JsonElement> answered = (await CalledAsync(
                    token,
                    [new Call("x:Account/set", arguments)],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        bool applied = push.State is MailboxState.Removed
            ? Destroyed(answered[0], account.Id)
            : Listed(answered[0], "updated", account.Id);

        return applied ? Result.Success() : Result.Failure(Fault());
    }

    // Whether the server created the account; false where it refused the create, which
    // a name that exists does.
    private async ValueTask<Result<bool>> CreatedAsync(
        string token,
        MailboxPush push,
        string local,
        string domainId,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<JsonElement> answered = (await CalledAsync(
                    token,
                    [
                        new Call("x:Account/set", new JsonObject
                        {
                            ["create"] = new JsonObject
                            {
                                ["mailbox"] = new JsonObject
                                {
                                    ["@type"] = "User",
                                    ["name"] = local,
                                    ["domainId"] = domainId,
                                    ["description"] = push.Mailbox.ToString(),
                                    ["credentials"] = new JsonObject(),
                                    ["roles"] = new JsonObject { ["@type"] = "User" },
                                    ["permissions"] = push.State is MailboxState.Enabled
                                        ? new JsonObject { ["@type"] = "Inherit" }
                                        : Merged(),
                                    ["encryptionAtRest"] = new JsonObject { ["@type"] = "Disabled" },
                                },
                            },
                        }),
                    ],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        if (Listed(answered[0], "created", "mailbox"))
        {
            return Result.Success(true);
        }

        return Listed(answered[0], "notCreated", "mailbox")
            ? Result.Success(false)
            : Result.Failure<bool>(Fault());
    }

    // D-166 215 point 4: the domain's identifier, found by its name; the name filter is
    // a text match, so only the domain named exactly counts, and none is a failure.
    private async ValueTask<Result<string>> DomainAsync(
        string token,
        string domain,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<JsonElement> domains = (await ListedAsync(
                    token,
                    "x:Domain",
                    new JsonObject { ["name"] = domain },
                    ["name"],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        foreach (JsonElement found in domains)
        {
            if (string.Equals(Text(found, "name"), domain, StringComparison.Ordinal)
                && Text(found, "id") is string id)
            {
                return Result.Success(id);
            }
        }

        return Result.Failure<string>(Fault());
    }

    // D-166 215 point 4: the account under the mailbox's name in its domain, where
    // there is one; the name filter is a text match, so only the name itself counts.
    private async ValueTask<Result<Account?>> AccountAsync(
        string token,
        string local,
        string domainId,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<JsonElement> accounts = (await ListedAsync(
                    token,
                    "x:Account",
                    new JsonObject { ["name"] = local, ["domainId"] = domainId },
                    ["name", "domainId", "description", "permissions"],
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<Account?>(failure);
        }

        Account? named = null;

        foreach (JsonElement account in accounts)
        {
            if (!string.Equals(Text(account, "name"), local, StringComparison.Ordinal)
                || !string.Equals(Text(account, "domainId"), domainId, StringComparison.Ordinal))
            {
                continue;
            }

            // One name in one domain is one account; an answer naming two does not read.
            if (named is not null || Text(account, "id") is not string id)
            {
                return Result.Failure<Account?>(Fault());
            }

            named = new Account(id, account);
        }

        return Result.Success(named);
    }

    // A query and, by result reference in the same request, a get of what it found,
    // page by page until a page finds nothing, so a server that pages its answers is
    // read whole.
    private async ValueTask<Result<IReadOnlyList<JsonElement>>> ListedAsync(
        string token,
        string type,
        JsonObject filter,
        string[] properties,
        CancellationToken cancellationToken)
    {
        var found = new List<JsonElement>();
        int position = 0;

        while (true)
        {
            Error? failure = null;
            var read = new JsonArray();

            foreach (string property in properties)
            {
                read.Add(property);
            }

            IReadOnlyList<JsonElement> answered = (await CalledAsync(
                        token,
                        [
                            new Call(type + "/query", new JsonObject
                            {
                                ["filter"] = filter.DeepClone(),
                                ["position"] = position,
                            }),
                            new Call(type + "/get", new JsonObject
                            {
                                ["#ids"] = new JsonObject
                                {
                                    ["resultOf"] = "0",
                                    ["name"] = type + "/query",
                                    ["path"] = "/ids",
                                },
                                ["properties"] = read,
                            }),
                        ],
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Withheld<IReadOnlyList<JsonElement>>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<IReadOnlyList<JsonElement>>(failure);
            }

            if (!answered[0].TryGetProperty("ids", out JsonElement ids)
                || ids.ValueKind is not JsonValueKind.Array
                || !answered[0].TryGetProperty("position", out JsonElement at)
                || !at.TryGetInt32(out int answeredAt)
                || answeredAt != position
                || !answered[1].TryGetProperty("list", out JsonElement list)
                || list.ValueKind is not JsonValueKind.Array)
            {
                return Result.Failure<IReadOnlyList<JsonElement>>(Fault());
            }

            int page = ids.GetArrayLength();

            if (page is 0)
            {
                return Result.Success<IReadOnlyList<JsonElement>>(found);
            }

            found.AddRange(list.EnumerateArray());
            position += page;
        }
    }

    // RFC 8620 section 3.3: one request, whose answers come back in the order of the
    // calls; a method error, or an answer out of step with its call, is a failure.
    private async ValueTask<Result<IReadOnlyList<JsonElement>>> CalledAsync(
        string bearer,
        IReadOnlyList<Call> calls,
        CancellationToken cancellationToken)
    {
        Uri endpoint = Volatile.Read(ref _endpoint)
            ?? throw new InvalidOperationException("The mail server's endpoint is not set.");

        var methodCalls = new JsonArray();

        for (int index = 0; index < calls.Count; index++)
        {
            methodCalls.Add(new JsonArray(
                calls[index].Method,
                calls[index].Arguments,
                index.ToString(CultureInfo.InvariantCulture)));
        }

        var request = new JsonObject
        {
            ["using"] = new JsonArray("urn:ietf:params:jmap:core", "urn:stalwart:jmap"),
            ["methodCalls"] = methodCalls,
        };

        JsonElement answer;

        try
        {
            using HttpClient requests = channel.CreateClient(Channel);
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(request.ToJsonString(), Encoding.UTF8, MediaTypeNames.Application.Json),
            };

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            using HttpResponseMessage response = await requests
                .SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<IReadOnlyList<JsonElement>>(Fault());
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var parsed = JsonDocument.Parse(body);

            answer = parsed.RootElement.Clone();
        }
        catch (HttpRequestException)
        {
            return Result.Failure<IReadOnlyList<JsonElement>>(Fault());
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's own timeout, which is the mail server not answering.
            return Result.Failure<IReadOnlyList<JsonElement>>(Fault());
        }
        catch (JsonException)
        {
            return Result.Failure<IReadOnlyList<JsonElement>>(Fault());
        }

        if (answer.ValueKind is not JsonValueKind.Object
            || !answer.TryGetProperty("methodResponses", out JsonElement responses)
            || responses.ValueKind is not JsonValueKind.Array
            || responses.GetArrayLength() != calls.Count)
        {
            return Result.Failure<IReadOnlyList<JsonElement>>(Fault());
        }

        var answered = new List<JsonElement>(calls.Count);
        int position = 0;

        foreach (JsonElement response in responses.EnumerateArray())
        {
            if (response.ValueKind is not JsonValueKind.Array
                || response.GetArrayLength() != 3
                || response[0].ValueKind is not JsonValueKind.String
                || !string.Equals(response[0].GetString(), calls[position].Method, StringComparison.Ordinal)
                || response[1].ValueKind is not JsonValueKind.Object
                || response[2].ValueKind is not JsonValueKind.String
                || !string.Equals(
                    response[2].GetString(),
                    position.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                return Result.Failure<IReadOnlyList<JsonElement>>(Fault());
            }

            answered.Add(response[1]);
            position++;
        }

        return Result.Success<IReadOnlyList<JsonElement>>(answered);
    }

    private sealed record Call(string Method, JsonObject Arguments);

    private sealed record Account(string Id, JsonElement Element);
}
