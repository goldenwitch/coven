// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Core.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Coven.Testing.Harness;

/// <summary>
/// Wrapper around <see cref="IHost"/> that provides access to virtual gateways
/// and test utilities for E2E testing.
/// </summary>
public sealed class E2ETestHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly VirtualConsoleIO? _console;
    private readonly VirtualOpenAIGateway? _openAI;
    private readonly VirtualDiscordGateway? _discord;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _shutdownTimeout;

    private CancellationTokenSource? _scopeCts;
    private DaemonScope? _daemonScope;

    internal E2ETestHost(
        IHost host,
        VirtualConsoleIO? console,
        VirtualOpenAIGateway? openAI,
        VirtualDiscordGateway? discord,
        TimeSpan startupTimeout,
        TimeSpan shutdownTimeout)
    {
        _host = host;
        _console = console;
        _openAI = openAI;
        _discord = discord;
        _startupTimeout = startupTimeout;
        _shutdownTimeout = shutdownTimeout;
        // JournalAccessor is initialized lazily after StartAsync creates the daemon scope
    }

    /// <summary>
    /// Gets the underlying service provider.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Gets the daemon scope's service provider, or the root provider if no scope is active.
    /// </summary>
    public IServiceProvider ScopedServices =>
        _daemonScope?.Scope.ServiceProvider ?? _host.Services;

    /// <summary>
    /// Gets the Coven runtime instance.
    /// </summary>
    public ICoven Coven => _host.Services.GetRequiredService<ICoven>();

    /// <summary>
    /// Gets the virtual console I/O.
    /// </summary>
    /// <exception cref="InvalidOperationException">Console was not configured for this host.</exception>
    public VirtualConsoleIO Console => _console ?? throw new InvalidOperationException(
        "Console not configured. Call UseVirtualConsole() on the builder.");

    /// <summary>
    /// Gets the virtual OpenAI gateway.
    /// </summary>
    /// <exception cref="InvalidOperationException">OpenAI was not configured for this host.</exception>
    public VirtualOpenAIGateway OpenAI => _openAI ?? throw new InvalidOperationException(
        "OpenAI not configured. Call UseVirtualOpenAI() on the builder.");

    /// <summary>
    /// Gets the virtual Discord gateway.
    /// </summary>
    /// <exception cref="InvalidOperationException">Discord was not configured for this host.</exception>
    public VirtualDiscordGateway Discord => _discord ?? throw new InvalidOperationException(
        "Discord not configured. Call UseVirtualDiscord() on the builder.");

    /// <summary>
    /// Gets whether the virtual console is configured.
    /// </summary>
    public bool HasConsole => _console is not null;

    /// <summary>
    /// Gets whether the virtual OpenAI gateway is configured.
    /// </summary>
    public bool HasOpenAI => _openAI is not null;

    /// <summary>
    /// Gets whether the virtual Discord gateway is configured.
    /// </summary>
    public bool HasDiscord => _discord is not null;

    /// <summary>
    /// Provides access to registered journals for test inspection.
    /// Uses the daemon scope's service provider for correct scoped service resolution.
    /// </summary>
    public JournalAccessor Journals => new(ScopedServices);

    /// <summary>
    /// Starts the host and the Coven runtime with daemons.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">Daemons did not reach ready state within timeout.</exception>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCts.CancelAfter(_startupTimeout);

        try
        {
            // 1. Start the underlying host
            await _host.StartAsync(startupCts.Token).ConfigureAwait(false);

            // 2. Create a daemon scope and start daemons directly using the internal API.
            // This bypasses the Ritual mechanism which completes immediately for Empty→Empty.
            _scopeCts = new CancellationTokenSource();
            _daemonScope = await CovenExecutionScope.BeginScopeAsync(_host.Services, _scopeCts.Token).ConfigureAwait(false);

            // Set the ambient scope so journal access works correctly
            CovenExecutionScope.SetCurrentScope(_daemonScope);

            // Set the scope for VirtualOpenAIGateway so it can resolve scriveners
            _openAI?.SetScopedProvider(_daemonScope.Scope.ServiceProvider);

            // 3. Wait for all daemons to reach Running status
            foreach (IDaemon daemon in _daemonScope.Daemons)
            {
                await WaitForDaemonRunningAsync(daemon, startupCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (startupCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            string daemonStates = FormatDaemonStates();
            throw new TimeoutException(
                $"E2E host failed to reach ready state within {_startupTimeout}. Daemon states: {daemonStates}");
        }
    }

    private static async Task WaitForDaemonRunningAsync(IDaemon daemon, CancellationToken cancellationToken)
    {
        while (daemon.Status is not Status.Running and not Status.Completed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    private string FormatDaemonStates()
    {
        if (_daemonScope is null)
        {
            return "(no scope)";
        }
        IEnumerable<string> states = _daemonScope.Daemons.Select(d => $"{d.GetType().Name}={d.Status}");
        return string.Join(", ", states);
    }

    /// <summary>
    /// Stops the host gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        shutdownCts.CancelAfter(_shutdownTimeout);

        try
        {
            // Signal input completion to unblock stdin/inbound pumps
            _console?.CompleteInput();
            _discord?.CompleteInbound();

            // Clear the ambient scopes first
            CovenExecutionScope.SetCurrentScope(null);
            _openAI?.SetScopedProvider(null);

            // End the daemon scope (shuts down daemons in reverse order)
            if (_daemonScope is not null)
            {
                await CovenExecutionScope.EndScopeAsync(_daemonScope, shutdownCts.Token).ConfigureAwait(false);
                _daemonScope = null;
            }

            await _host.StopAsync(shutdownCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (shutdownCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"E2E host shutdown timed out after {_shutdownTimeout}. " +
                "Possible deadlock in message pump.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Force disposal even if shutdown times out
        }
        finally
        {
            _scopeCts?.Dispose();
            _host.Dispose();

            if (_console is not null)
            {
                await _console.DisposeAsync().ConfigureAwait(false);
            }

            if (_discord is not null)
            {
                await _discord.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
