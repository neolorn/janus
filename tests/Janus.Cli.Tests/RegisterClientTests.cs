using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>register-client</c> command does to a bootstrapped deployment: a client
/// enters the registry from the server, the library draws its secret and holds it
/// wrapped under the deployment's data key, and registering a client the registry holds
/// changes it and leaves its secret alone (AUTH-OIDC-001, OPS-SEC-002, D-166 340).
/// </summary>
[Trait("kind", "integration")]
public sealed class RegisterClientTests(BootstrappedDeployment deployment) : IClassFixture<BootstrappedDeployment>
{
    private const string Redirect = "https://mail.example.test/callback";

    /// <summary>
    /// AUTH-OIDC-001 AC4: the mail-server client is registered as the deployment is
    /// stood up with no secret supplied: the library draws one, holds it wrapped, and
    /// writes the registration down under the command's principal with nothing of it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredWithNoSecretSuppliedAsync()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        Invocation run = await RegisteredAsync(Arguments("mail", "protocol", Redirect));
        DateTimeOffset after = DateTimeOffset.UtcNow;

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        (string Kind, string Redirect, string[] Scopes, byte[] Secret, DateTimeOffset IssuedAt, byte[]? Previous) held =
            await connection.QuerySingleAsync<(string, string, string[], byte[], DateTimeOffset, byte[]?)>(
                """
                SELECT kind, redirect, scopes, secret, secret_issued_at, previous_secret
                FROM identity.oidc_clients WHERE client_id = 'mail'
                """);
        (string? Kind, string? Changed, string Reason, bool Secret) recorded = await connection
            .QuerySingleAsync<(string?, string?, string, bool)>(
                """
                SELECT details->>'kind', details->>'changed', principal_reason, details::text ILIKE '%secret%'
                FROM identity.audit_records
                WHERE action = 'auth.oidc.clientregistered' AND principal = 'register-client'
                  AND details->>'client' = 'mail'
                """);
        byte[] secret = Unwrapped(held.Secret, await DeploymentKeyAsync(connection));

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("""{"registered":"mail"}""", run.Output.Trim());
        Assert.Equal(("protocol", Redirect), (held.Kind, held.Redirect));
        Assert.Equal(["openid", "email", "offline_access"], held.Scopes);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", Encoding.ASCII.GetString(secret));
        Assert.NotEqual(secret, held.Secret);
        Assert.InRange(held.IssuedAt, before.AddSeconds(-1), after);
        Assert.Null(held.Previous);
        Assert.Equal(("protocol", "false", "AUTH-OIDC-001", false), recorded);
    }

    /// <summary>
    /// OPS-SEC-002: registering a client the registry holds changes its kind and
    /// destination and leaves its secret, and when the secret was drawn, as they were.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_ARegistrationThatChangesAClientKeepsItsSecretAsync()
    {
        const string moved = "https://mail.example.test/moved";

        Assert.Equal(0, (await RegisteredAsync(Arguments("changed", "browser-application", Redirect))).ExitCode);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        (byte[] Secret, DateTimeOffset IssuedAt) first = await HeldAsync(connection);

        Invocation run = await RegisteredAsync(Arguments("changed", "protocol", moved));

        (byte[] Secret, DateTimeOffset IssuedAt) second = await HeldAsync(connection);
        (string Kind, string Redirect, byte[]? Previous) changed = await connection
            .QuerySingleAsync<(string, string, byte[]?)>(
                "SELECT kind, redirect, previous_secret FROM identity.oidc_clients WHERE client_id = 'changed'");
        IEnumerable<string> recorded = await connection.QueryAsync<string>(
            """
            SELECT details->>'changed' FROM identity.audit_records
            WHERE action = 'auth.oidc.clientregistered' AND details->>'client' = 'changed'
            ORDER BY id
            """);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(first.Secret, second.Secret);
        Assert.Equal(first.IssuedAt, second.IssuedAt);
        Assert.Equal(("protocol", moved), (changed.Kind, changed.Redirect));
        Assert.Null(changed.Previous);
        Assert.Equal(["false", "true"], recorded);
    }

    /// <summary>
    /// AUTH-OIDC-001: a kind that is not one of the two, an argument the command does not
    /// take and one it needs but was not given are each refused by name before the
    /// database is reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AnArgumentTheCommandCannotTakeIsRefusedAsync()
    {
        Invocation kind = await RegisteredAsync(Arguments("refused", "public", Redirect));
        Invocation unknown = await RegisteredAsync(
            [.. Arguments("refused", "protocol", Redirect), "--secret", "a-secret-of-the-operator"]);
        Invocation missing = await RegisteredAsync(
            ["register-client", "--client", "refused", "--name", "Refused", "--kind", "protocol", "--redirect", Redirect]);

        Assert.Equal(("api.request.malformed", "kind"), Refusal(kind));
        Assert.Equal(("api.request.malformed", "--secret"), Refusal(unknown));
        Assert.Equal(("api.request.malformed", "scopes"), Refusal(missing));
    }

    /// <summary>
    /// AUTH-OIDC-006, API-REDIR-001 AC3 (D-166, 145): a plaintext return address off the
    /// loopback is refused by the command as startup would refuse it, naming the client,
    /// and nothing is registered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_APlaintextReturnAddressIsNotRegisteredAsync()
    {
        Invocation run = await RegisteredAsync(Arguments("plain", "protocol", "http://mail.example.test/callback"));

        using var refusal = JsonDocument.Parse(run.Error);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Equal("model.startup.redirectclient", refusal.RootElement.GetProperty("code").GetString());
        Assert.Equal("plain", refusal.RootElement.GetProperty("details").GetProperty("client").GetString());

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(
            0,
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.oidc_clients WHERE client_id = 'plain'"));
    }

    /// <summary>
    /// OPS-SEC-003 AC3 (D-183): a command that meets a value wrapped under a
    /// key-encryption key version its document does not hold ends with exit code 1 and the
    /// code, the key and the version on standard error, and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC3_AValueUnderAVersionTheDocumentLacksEndsTheCommandWithTheCodeAsync()
    {
        JsonObject lacking = Invocation.Keys(deployment.ConnectionString);

        lacking["keyEncryptionKeys"] = new JsonObject
        {
            ["current"] = 2,
            ["versions"] = new JsonObject { ["2"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
        };

        Invocation run = await Invocation.PipedAsync(Arguments("unheld", "protocol", Redirect), lacking);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(
            """{"code":"model.startup.secretunavailable","details":{"key":"keyEncryptionKeys","version":1}}""",
            run.Error.Trim());
        Assert.Equal(
            0,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM identity.oidc_clients WHERE client_id = 'unheld'"));
    }

    private static IReadOnlyList<string> Arguments(string client, string kind, string redirect) =>
    [
        "register-client",
        "--client", client,
        "--name", "The " + client,
        "--kind", kind,
        "--redirect", redirect,
        "--scopes", "openid email offline_access",
    ];

    private static (string?, string?) Refusal(Invocation run)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return (
            refusal.RootElement.GetProperty("code").GetString(),
            refusal.RootElement.GetProperty("details").GetProperty("member").GetString());
    }

    // D-172: the deployment's data key, the row of the subject-key table under the max
    // UUID, wrapped under the key-encryption key the deployment was stood up with.
    private static async Task<byte[]> DeploymentKeyAsync(NpgsqlConnection connection) =>
        Unwrapped(
            await connection.QuerySingleAsync<byte[]>(
                "SELECT wrapped_key FROM identity.subject_keys WHERE subject = @reserved",
                new { reserved = Guid.AllBitsSet }),
            Convert.FromBase64String(Invocation.KeyEncryptionKey));

    private static byte[] Unwrapped(byte[] wrapped, byte[] wrappingKey)
    {
        using var aes = Aes.Create();
        aes.Key = wrappingKey;

        return aes.DecryptKeyWrapPadded(wrapped);
    }

    private static async Task<(byte[] Secret, DateTimeOffset IssuedAt)> HeldAsync(NpgsqlConnection connection) =>
        await connection.QuerySingleAsync<(byte[], DateTimeOffset)>(
            "SELECT secret, secret_issued_at FROM identity.oidc_clients WHERE client_id = 'changed'");

    // The command run with the keys piped as the operator pipes them, and nothing else.
    private Task<Invocation> RegisteredAsync(IReadOnlyList<string> arguments) =>
        Invocation.PipedAsync(arguments, Invocation.Keys(deployment.ConnectionString));
}
