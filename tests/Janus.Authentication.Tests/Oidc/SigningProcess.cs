using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// One process of a deployment as the signing keys see it: its own credential source,
/// reading the stored keys through a scope of its own at each change, on a clock of its
/// own, over the keys and the configuration every process of the deployment shares.
/// </summary>
internal sealed class SigningProcess : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IConfigurationStore _configuration;

    /// <summary>
    /// Starts the process over the stored keys.
    /// </summary>
    /// <param name="keys">The keys the deployment holds.</param>
    /// <param name="configuration">The deployment's configuration.</param>
    /// <param name="clock">The clock the process runs on.</param>
    public SigningProcess(SigningKeyStoreInMemory keys, ConfigurationInMemory configuration, TimeProvider clock)
    {
        _configuration = configuration;

        var services = new ServiceCollection();

        _ = services.AddSingleton<ISigningKeyStore>(keys);
        _ = services.AddSingleton<IConfigurationStore>(configuration);
        _ = services.AddSingleton(clock);
        _ = services.AddSingleton<IUnitOfWork>(Work);
        _ = services.AddScoped<SigningKeys>();
        _ = services.AddSingleton(provider => new SigningCredentialSource(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock));

        _services = services.BuildServiceProvider();
        Source = _services.GetRequiredService<SigningCredentialSource>();
    }

    /// <summary>
    /// The transactions the process opened and committed.
    /// </summary>
    public UnitOfWorkInMemory Work { get; } = new();

    /// <summary>
    /// The process's credential source.
    /// </summary>
    public SigningCredentialSource Source { get; }

    /// <summary>
    /// Reads the signing keys, as every token signed, every request for the key set and
    /// every validation does.
    /// </summary>
    /// <returns>The set the read leaves.</returns>
    public async Task<SigningKeySet> ReadAsync() =>
        (await Source.ReadAsync(_configuration, TestContext.Current.CancellationToken)).Match(
            set => set,
            error => throw new InvalidOperationException(error.Code.ToString()));

    /// <summary>
    /// What a token is signed with.
    /// </summary>
    /// <param name="accessToken">Whether the token is an access token.</param>
    /// <returns>The credential.</returns>
    public async Task<SigningCredentials> SigningAsync(bool accessToken) =>
        (await Source.SigningAsync(_configuration, accessToken, TestContext.Current.CancellationToken)).Match(
            credentials => credentials,
            error => throw new InvalidOperationException(error.Code.ToString()));

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await Work.DisposeAsync();
    }
}
