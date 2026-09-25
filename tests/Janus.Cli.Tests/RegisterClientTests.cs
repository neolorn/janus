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
/// enters the registry from the server, its secret piped with the keys and held as what
/// it hashes to, and a new secret leaves the one it replaced for the overlap
/// (AUTH-OIDC-001, OPS-SEC-002, entry 340).
/// </summary>
[Trait("kind", "integration")]
public sealed class RegisterClientTests(BootstrappedDeployment deployment) : IClassFixture<BootstrappedDeployment>
{
    private const string Redirect = "https://mail.example.test/callback";

    private const string Secret = "the-secret-the-mail-server-presents";

    /// <summary>
    /// AUTH-OIDC-001 AC4: the mail-server client is registered as the deployment is
    /// stood up, the registry holding what its secret hashes to, and the registration
    /// written down under the command's principal.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredAsTheDeploymentIsStoodUpAsync()
    {
        Invocation run = await RegisteredAsync("mail", "protocol", Secret);

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        (string Kind, string Redirect, string[] Scopes, byte[] Secret, byte[]? Previous) held = await connection
            .QuerySingleAsync<(string, string, string[], byte[], byte[]?)>(
                """
                SELECT kind, redirect, scopes, secret, previous_secret
                FROM identity.oidc_clients WHERE client_id = 'mail'
                """);
        (string? Kind, string? Changed, string Reason) recorded = await connection
            .QuerySingleAsync<(string?, string?, string)>(
                """
                SELECT details->>'kind', details->>'changed', principal_reason
                FROM identity.audit_records
                WHERE action = 'auth.oidc.clientregistered' AND principal = 'register-client'
                  AND details->>'client' = 'mail'
                """);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("""{"registered":"mail"}""", run.Output.Trim());
        Assert.Equal(("protocol", Redirect), (held.Kind, held.Redirect));
        Assert.Equal(["openid", "email", "offline_access"], held.Scopes);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(Secret)), held.Secret);
        Assert.Null(held.Previous);
        Assert.Equal(("protocol", "false", "AUTH-OIDC-001"), recorded);
    }

    /// <summary>
    /// OPS-SEC-002 AC2: registering the client again with a new secret is how a secret
    /// is rotated, and the one it replaced stays accepted for the access-token lifetime
    /// and five minutes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_002_AC2_RegisteringANewSecretKeepsTheReplacedOneThroughTheOverlapAsync()
    {
        const string replacement = "the-secret-that-replaced-the-first-one";

        await RegisteredAsync("rotated", "browser-application", Secret);

        DateTimeOffset before = DateTimeOffset.UtcNow;
        Invocation run = await RegisteredAsync("rotated", "browser-application", replacement);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        (byte[] Secret, byte[] Previous, DateTimeOffset Until) held = await connection
            .QuerySingleAsync<(byte[], byte[], DateTimeOffset)>(
                """
                SELECT secret, previous_secret, previous_secret_until
                FROM identity.oidc_clients WHERE client_id = 'rotated'
                """);
        IEnumerable<string> changed = await connection.QueryAsync<string>(
            """
            SELECT details->>'changed' FROM identity.audit_records
            WHERE action = 'auth.oidc.clientregistered' AND details->>'client' = 'rotated'
            ORDER BY id
            """);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(replacement)), held.Secret);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(Secret)), held.Previous);
        Assert.InRange(held.Until, before + TimeSpan.FromMinutes(15), after + TimeSpan.FromMinutes(15));
        Assert.Equal(["false", "true"], changed);
    }

    /// <summary>
    /// AUTH-OIDC-001 and OPS-SEC-001: the secret comes with the keys and never as an
    /// argument, so a document that carries none, or one the registry would not hold, is
    /// refused naming it and nothing is registered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_ARegistrationWithoutAUsableSecretIsRefusedAsync()
    {
        Invocation absent = await Invocation.PipedAsync(
            Arguments("unsecured", "protocol"),
            Invocation.Keys(deployment.ConnectionString));
        Invocation shortSecret = await RegisteredAsync("unsecured", "protocol", "short");

        await using NpgsqlConnection connection = await deployment.OpenAsync();

        Assert.Equal(1, absent.ExitCode);
        Assert.Equal(("api.request.malformed", "clientSecret"), Refusal(absent));
        Assert.Equal(("api.request.malformed", "clientSecret"), Refusal(shortSecret));
        Assert.Equal(0, await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.oidc_clients WHERE client_id = 'unsecured'"));
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
        Invocation kind = await RegisteredAsync("refused", "public", Secret);
        Invocation unknown = await Invocation.PipedAsync(
            [.. Arguments("refused", "protocol"), "--secret", Secret],
            Keys(Secret));
        Invocation missing = await Invocation.PipedAsync(
            ["register-client", "--client", "refused", "--name", "Refused", "--kind", "protocol", "--redirect", Redirect],
            Keys(Secret));

        Assert.Equal(("api.request.malformed", "kind"), Refusal(kind));
        Assert.Equal(("api.request.malformed", "--secret"), Refusal(unknown));
        Assert.Equal(("api.request.malformed", "scopes"), Refusal(missing));
    }

    private static IReadOnlyList<string> Arguments(string client, string kind) =>
    [
        "register-client",
        "--client", client,
        "--name", "The " + client,
        "--kind", kind,
        "--redirect", Redirect,
        "--scopes", "openid email offline_access",
    ];

    private static (string?, string?) Refusal(Invocation run)
    {
        using var refusal = JsonDocument.Parse(run.Error);

        return (
            refusal.RootElement.GetProperty("code").GetString(),
            refusal.RootElement.GetProperty("details").GetProperty("member").GetString());
    }

    private JsonObject Keys(string secret)
    {
        JsonObject keys = Invocation.Keys(deployment.ConnectionString);

        keys["clientSecret"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));

        return keys;
    }

    private Task<Invocation> RegisteredAsync(string client, string kind, string secret) =>
        Invocation.PipedAsync(Arguments(client, kind), Keys(secret));
}
