// SPDX-License-Identifier: BUSL-1.1

using Coven;
using Coven.Agents;
using Coven.Agents.Claude;
using Coven.Chat;
using Coven.Chat.Console;
using Coven.Core.Builder;
using Coven.Core.Covenants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Configuration
ConsoleClientConfig consoleConfig = new()
{
    InputSender = "console",
    OutputSender = "BOT"
};

ClaudeClientConfig claudeConfig = new()
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "", // set your key or use env var
    Model = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "claude-sonnet-4-20250514",
    SystemPrompt = "You are a helpful assistant."
};

// ───────────────────────────────────────────────────────────────────────────
// DECLARATIVE COVENANT CONFIGURATION
// ───────────────────────────────────────────────────────────────────────────

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(b => b.AddConsole());

builder.Services.BuildCoven(coven =>
{
    BranchManifest chat = coven.UseConsoleChat(consoleConfig);
    BranchManifest agents = coven.UseClaudeAgents(claudeConfig);

    coven.Covenant()
        .Connect(chat)
        .Connect(agents)
        .Routes(c =>
        {
            // Chat → Agents: incoming messages become prompts
            c.Route<ChatAfferent, AgentPrompt>(
                (msg, ct) => Task.FromResult(
                    new AgentPrompt(msg.Sender, msg.Text)));

            // Agents → Chat: responses become outgoing messages
            c.Route<AgentResponse, ChatEfferent>(
                (r, ct) => Task.FromResult(
                    new ChatEfferent("BOT", r.Text)));

            // Thoughts are terminal (displayed for visibility)
            c.Route<AgentThought, ChatEfferent>(
                (t, ct) => Task.FromResult(
                    new ChatEfferent("THINKING", $"[Thinking] {t.Text}")));
        });
});

// Add background service to keep daemons running for the host lifetime
builder.Services.AddHostedService<CovenDaemonHostedService>();

IHost host = builder.Build();
await host.RunAsync();

/// <summary>
/// Hosted service that starts Coven daemons and keeps them running for the application lifetime.
/// This is needed because Ritual&lt;Empty, Empty&gt; completes immediately and shuts down daemons.
/// </summary>
file sealed class CovenDaemonHostedService(IServiceProvider services, ILogger<CovenDaemonHostedService> logger) : BackgroundService
{
#pragma warning disable CA1848, CA1873 // For toys, simpler logging is acceptable
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Coven daemons...");

        // Create a service scope to resolve scoped daemons
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        // Resolve and start all registered daemons
        IEnumerable<IDaemon> daemons = scope.ServiceProvider.GetServices<IDaemon>();
        List<IDaemon> startedDaemons = [];

        try
        {
            foreach (IDaemon daemon in daemons)
            {
                await daemon.Start(stoppingToken);
                startedDaemons.Add(daemon);
                logger.LogInformation("Started daemon: {DaemonType}", daemon.GetType().Name);
            }

            logger.LogInformation("All daemons started. Waiting for shutdown signal...");

            // Keep running until cancellation requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shutdown signal received. Stopping daemons...");
        }
        finally
        {
            // Stop daemons in reverse order
            foreach (IDaemon daemon in startedDaemons.AsEnumerable().Reverse())
            {
                try
                {
                    await daemon.Shutdown(CancellationToken.None);
                    logger.LogInformation("Stopped daemon: {DaemonType}", daemon.GetType().Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error stopping daemon: {DaemonType}", daemon.GetType().Name);
                }
            }
        }
    }
#pragma warning restore CA1848, CA1873
}
