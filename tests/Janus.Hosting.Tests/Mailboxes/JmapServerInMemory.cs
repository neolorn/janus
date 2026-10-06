using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Hosting.Tests.Mailboxes;

/// <summary>
/// The mail server's JMAP management interface as a test holds it: domains, accounts and
/// app passwords, the management key it accepts and the people's tokens it knows,
/// answering each request as the object reference describes and recording every one.
/// </summary>
/// <remarks>
/// A create whose name exists in its domain is refused. The name filter is a text match,
/// as the server's is, and a query answers one page at a time.
/// </remarks>
internal sealed class JmapServerInMemory : HttpMessageHandler
{
    private readonly Dictionary<string, string> _domains = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonObject> _accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _people = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<JsonObject>> _passwords = new(StringComparer.Ordinal);
    private readonly List<Received> _received = [];
    private int _next;

    /// <summary>
    /// The management key it accepts.
    /// </summary>
    public string ManagementKey { get; init; } = "management-key";

    /// <summary>
    /// How many identifiers a query answers at most.
    /// </summary>
    public int PageSize { get; init; } = 2;

    /// <summary>
    /// How many account queries still find nothing, as a query made just before another
    /// client's create does.
    /// </summary>
    public int QueriesMissing { get; set; }

    /// <summary>
    /// What it answers every request with instead of reading it, where a test says.
    /// </summary>
    public Func<HttpResponseMessage>? Answer { get; set; }

    /// <summary>
    /// Every request, in order.
    /// </summary>
    public IReadOnlyList<Received> Requests => _received;

    /// <summary>
    /// Holds a domain.
    /// </summary>
    /// <param name="name">Its name.</param>
    /// <returns>Its identifier.</returns>
    public string Domain(string name)
    {
        string id = Next("d");

        _domains[id] = name;

        return id;
    }

    /// <summary>
    /// Holds an account someone made at the server.
    /// </summary>
    /// <param name="address">Its address.</param>
    /// <param name="description">Its description, where it has one.</param>
    /// <param name="permissions">Its permissions, as the server encodes them.</param>
    /// <param name="kind">Its kind, <c>User</c> or <c>Group</c>.</param>
    /// <returns>Its identifier.</returns>
    public string Account(string address, string? description, JsonObject permissions, string kind = "User")
    {
        int at = address.LastIndexOf('@');
        string domainId = _domains.Single(domain => domain.Value == address[(at + 1)..]).Key;
        string id = Next("a");

        _accounts[id] = new JsonObject
        {
            ["@type"] = kind,
            ["name"] = address[..at],
            ["domainId"] = domainId,
            ["emailAddress"] = address,
            ["description"] = description,
            ["credentials"] = new JsonObject(),
            ["roles"] = new JsonObject { ["@type"] = "User" },
            ["permissions"] = permissions,
            ["encryptionAtRest"] = new JsonObject { ["@type"] = "Disabled" },
        };

        return id;
    }

    /// <summary>
    /// Every account it holds at an address, as it holds it.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The accounts.</returns>
    public IReadOnlyList<JsonObject> At(string address) =>
        [.. _accounts.Values.Where(account => (string?)account["emailAddress"] == address)];

    /// <summary>
    /// Knows a person's token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="person">Whose it is.</param>
    public void Knows(string token, string person) => _people[token] = person;

    /// <summary>
    /// The app passwords it holds for a person.
    /// </summary>
    /// <param name="person">Whose.</param>
    /// <returns>What it holds.</returns>
    public IReadOnlyList<JsonObject> PasswordsOf(string person) =>
        _passwords.TryGetValue(person, out List<JsonObject>? held) ? held : [];

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string? bearer = request.Headers.Authorization is { Scheme: "Bearer" } authorization
            ? authorization.Parameter
            : null;
        var read = JsonNode.Parse(body) as JsonObject;

        _received.Add(new Received(request.Method, request.RequestUri, bearer, read));

        if (Answer is not null)
        {
            return Answer();
        }

        if (read?["methodCalls"] is not JsonArray calls)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        var responses = new JsonArray();

        foreach (JsonNode? call in calls)
        {
            string method = (string)call![0]!;
            var arguments = (JsonObject)call[1]!.DeepClone();
            string callId = (string)call[2]!;

            if (arguments["#ids"] is JsonObject reference)
            {
                arguments.Remove("#ids");
                arguments["ids"] = responses
                    .Single(response => (string?)response![2] == (string?)reference["resultOf"])![1]!["ids"]!
                    .DeepClone();
            }

            bool management = !method.StartsWith("x:AppPassword/", StringComparison.Ordinal);

            if (management ? bearer != ManagementKey : bearer is null || !_people.ContainsKey(bearer))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            JsonObject answered = method switch
            {
                "x:Domain/query" => Queried(
                    _domains.Where(domain => Matches(domain.Value, arguments["filter"]?["name"])).Select(domain => domain.Key),
                    arguments),
                "x:Domain/get" => Got(
                    arguments,
                    id => _domains.TryGetValue(id, out string? name) ? new JsonObject { ["name"] = name } : null),
                "x:Account/query" => Queried(AccountsFound(arguments), arguments),
                "x:Account/get" => Got(arguments, id => _accounts.GetValueOrDefault(id)),
                "x:Account/set" => AccountsSet(arguments),
                "x:AppPassword/get" => PasswordsGot(_people[bearer!]),
                "x:AppPassword/set" => PasswordsSet(_people[bearer!], arguments),
                _ => throw new InvalidOperationException("The server knows no method " + method + "."),
            };

            responses.Add(new JsonArray(method, answered, callId));
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                new JsonObject { ["methodResponses"] = responses, ["sessionState"] = "0" }.ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
    }

    private static bool Matches(string value, JsonNode? filter) =>
        filter is null || value.Contains((string)filter!, StringComparison.OrdinalIgnoreCase);

    private static JsonObject Got(JsonObject arguments, Func<string, JsonObject?> find)
    {
        var list = new JsonArray();
        var notFound = new JsonArray();
        string[] wanted = arguments["properties"] is JsonArray properties
            ? [.. properties.Select(property => (string)property!)]
            : [];

        foreach (JsonNode? id in (JsonArray)arguments["ids"]!)
        {
            if (find((string)id!) is not JsonObject found)
            {
                notFound.Add((string)id!);

                continue;
            }

            var entry = new JsonObject { ["id"] = (string)id! };

            if (found["@type"] is JsonNode kind)
            {
                entry["@type"] = kind.DeepClone();
            }

            foreach (string property in wanted)
            {
                entry[property] = found[property]?.DeepClone();
            }

            list.Add(entry);
        }

        return new JsonObject { ["list"] = list, ["notFound"] = notFound };
    }

    private static void Patched(JsonObject account, string path, JsonNode? value)
    {
        string[] parts = path.Split('/');
        JsonObject target = account;

        // RFC 8620 section 5.3: every part of the path before the last must exist.
        foreach (string part in parts[..^1])
        {
            target = target[part] as JsonObject
                ?? throw new InvalidOperationException("The patch path " + path + " names nothing.");
        }

        if (value is null)
        {
            target.Remove(parts[^1]);
        }
        else
        {
            target[parts[^1]] = value.DeepClone();
        }
    }

    private JsonObject Queried(IEnumerable<string> ids, JsonObject arguments)
    {
        int position = (int?)arguments["position"] ?? 0;
        var page = new JsonArray();

        foreach (string id in ids.Order(StringComparer.Ordinal).Skip(position).Take(PageSize))
        {
            page.Add(id);
        }

        return new JsonObject { ["ids"] = page, ["position"] = position };
    }

    private IEnumerable<string> AccountsFound(JsonObject arguments)
    {
        if (QueriesMissing > 0 && arguments["filter"]?["name"] is not null)
        {
            QueriesMissing--;

            return [];
        }

        return _accounts
            .Where(account => Matches((string)account.Value["name"]!, arguments["filter"]?["name"]))
            .Where(account => arguments["filter"]?["domainId"] is not JsonNode domain
                || (string?)account.Value["domainId"] == (string?)domain)
            .Select(account => account.Key);
    }

    private JsonObject AccountsSet(JsonObject arguments)
    {
        var answer = new JsonObject();

        if (arguments["create"] is JsonObject create)
        {
            var created = new JsonObject();
            var notCreated = new JsonObject();

            foreach ((string key, JsonNode? value) in create)
            {
                var account = (JsonObject)value!.DeepClone();
                string domainId = (string)account["domainId"]!;
                string name = (string)account["name"]!;

                if (_accounts.Values.Any(held => (string?)held["name"] == name && (string?)held["domainId"] == domainId))
                {
                    notCreated[key] = new JsonObject { ["type"] = "invalidProperties", ["properties"] = new JsonArray("name") };

                    continue;
                }

                string id = Next("a");
                string address = name + "@" + _domains[domainId];

                account["emailAddress"] = address;
                _accounts[id] = account;
                created[key] = new JsonObject { ["id"] = id, ["emailAddress"] = address };
            }

            answer["created"] = created;
            answer["notCreated"] = notCreated;
        }

        if (arguments["update"] is JsonObject update)
        {
            var updated = new JsonObject();
            var notUpdated = new JsonObject();

            foreach ((string id, JsonNode? patch) in update)
            {
                if (!_accounts.TryGetValue(id, out JsonObject? account))
                {
                    notUpdated[id] = new JsonObject { ["type"] = "notFound" };

                    continue;
                }

                foreach ((string path, JsonNode? value) in (JsonObject)patch!)
                {
                    Patched(account, path, value);
                }

                updated[id] = null;
            }

            answer["updated"] = updated;
            answer["notUpdated"] = notUpdated;
        }

        if (arguments["destroy"] is JsonArray destroy)
        {
            var destroyed = new JsonArray();
            var notDestroyed = new JsonObject();

            foreach (string id in destroy.Select(id => (string)id!))
            {
                if (_accounts.Remove(id))
                {
                    destroyed.Add(id);
                }
                else
                {
                    notDestroyed[id] = new JsonObject { ["type"] = "notFound" };
                }
            }

            answer["destroyed"] = destroyed;
            answer["notDestroyed"] = notDestroyed;
        }

        return answer;
    }

    private JsonObject PasswordsGot(string person) =>
        new()
        {
            ["list"] = new JsonArray([.. PasswordsOf(person).Select(password => (JsonNode?)new JsonObject
            {
                ["id"] = password["id"]!.DeepClone(),
                ["description"] = password["description"]!.DeepClone(),
                ["createdAt"] = password["createdAt"]!.DeepClone(),
                ["expiresAt"] = password["expiresAt"]?.DeepClone(),
            })]),
            ["notFound"] = new JsonArray(),
        };

    private JsonObject PasswordsSet(string person, JsonObject arguments)
    {
        var answer = new JsonObject();

        if (!_passwords.TryGetValue(person, out List<JsonObject>? held))
        {
            held = [];
            _passwords[person] = held;
        }

        if (arguments["create"] is JsonObject create)
        {
            var created = new JsonObject();

            foreach ((string key, JsonNode? value) in create)
            {
                var password = (JsonObject)value!.DeepClone();
                string id = Next("p");

                password["id"] = id;
                password["secret"] = "generated-" + id;
                password["createdAt"] = "2026-09-30T12:00:00Z";
                held.Add(password);
                created[key] = new JsonObject
                {
                    ["id"] = id,
                    ["secret"] = (string)password["secret"]!,
                    ["createdAt"] = (string)password["createdAt"]!,
                };
            }

            answer["created"] = created;
            answer["notCreated"] = new JsonObject();
        }

        if (arguments["destroy"] is JsonArray destroy)
        {
            var destroyed = new JsonArray();
            var notDestroyed = new JsonObject();

            foreach (string id in destroy.Select(id => (string)id!))
            {
                if (held.RemoveAll(password => (string?)password["id"] == id) > 0)
                {
                    destroyed.Add(id);
                }
                else
                {
                    notDestroyed[id] = new JsonObject { ["type"] = "notFound" };
                }
            }

            answer["destroyed"] = destroyed;
            answer["notDestroyed"] = notDestroyed;
        }

        return answer;
    }

    private string Next(string prefix) =>
        prefix + (++_next).ToString("D4", CultureInfo.InvariantCulture);

    /// <summary>
    /// One request as the server received it.
    /// </summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Address">Where it was sent.</param>
    /// <param name="Bearer">The bearer token it carried, where it carried one.</param>
    /// <param name="Body">The JMAP request, where the body read as one.</param>
    internal sealed record Received(HttpMethod Method, Uri? Address, string? Bearer, JsonObject? Body);
}
