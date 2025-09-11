// SPDX-License-Identifier: BUSL-1.1

using Microsoft.Extensions.DependencyInjection;
using Coven.Chat;
using Coven.Spellcasting.Agents.Codex.Di;
using Coven.Spellcasting.Agents.Codex.MCP;
using Coven.Spellcasting.Agents;

namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

/// <summary>
/// Fluent DI helper for tests to compose a CodexCliAgent with exact seams.
/// Allows providing concrete instances or relying on defaults where omitted.
/// </summary>
public sealed class CodexAgentTestHost<TMessageFormat> : IDisposable where TMessageFormat : notnull
{
    private readonly ServiceCollection _services = new();
    private readonly CodexCliAgentRegistrationOptions _options = new();
    private ServiceProvider? _provider;
    private string? _tempWorkspace;

    public CodexAgentTestHost()
    {
        // Default: in-memory scrivener to capture output in tests
        _services.AddSingleton<IScrivener<TMessageFormat>, InMemoryScrivener<TMessageFormat>>();
    }

    public CodexAgentTestHost<TMessageFormat> Configure(Action<CodexCliAgentRegistrationOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        configure(_options);
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> UseTempWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"coven_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _options.WorkspaceDirectory = path;
        _tempWorkspace = path;
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> WithScrivener(IScrivener<TMessageFormat> scrivener)
    {
        if (scrivener is null) throw new ArgumentNullException(nameof(scrivener));
        _services.AddSingleton(scrivener);
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> WithHost(IMcpServerHost host)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        _services.AddSingleton(host);
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> WithProcessFactory(Processes.ICodexProcessFactory factory)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        _services.AddSingleton(factory);
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> WithTailFactory(ITailMuxFactory factory)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        _services.AddSingleton(factory);
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> WithConfigWriter(Config.ICodexConfigWriter writer)
    {
        if (writer is null) throw new ArgumentNullException(nameof(writer));
        _services.AddSingleton(writer);
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> WithRolloutResolver(Rollout.IRolloutPathResolver resolver)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        _services.AddSingleton(resolver);
        return this;
    }

    public CodexAgentTestHost<TMessageFormat> WithSpells(IEnumerable<object> spells)
    {
        _options.Spells = spells ?? throw new ArgumentNullException(nameof(spells));
        return this;
    }

    /// <summary>
    /// Build the provider and register the agent using the configured options and services.
    /// </summary>
        public CodexAgentTestHost<TMessageFormat> Build()
        {
            if (_provider is not null) return this; // idempotent

            if (typeof(TMessageFormat) == typeof(string))
            {
                _services.AddCodexCliAgent(o =>
                {
                    o.ExecutablePath = _options.ExecutablePath;
                    o.WorkspaceDirectory = _options.WorkspaceDirectory;
                    o.ShimExecutablePath = _options.ShimExecutablePath;
                    o.Spells = _options.Spells;
                });
            }
            else
            {
                // For non-string message formats, require a translator to be registered in services.
                // Tests can register ICodexRolloutTranslator<TMessageFormat> before Build().
                _services.AddCodexCliAgent<TMessageFormat, DummyTranslator>(o =>
                {
                    o.ExecutablePath = _options.ExecutablePath;
                    o.WorkspaceDirectory = _options.WorkspaceDirectory;
                    o.ShimExecutablePath = _options.ShimExecutablePath;
                    o.Spells = _options.Spells;
                });
            }

            _provider = _services.BuildServiceProvider();
            return this;
        }

        // Placeholder translator to satisfy the generic constraint when TMessageFormat != string.
        // Real translator should be provided via DI; this one will never be resolved because
        // AddCodexCliAgent<TMessage, TTranslator> first tries to resolve ICodexRolloutTranslator<TMessage>
        // from the service provider before creating TTranslator.
        private sealed class DummyTranslator : Coven.Spellcasting.Agents.Codex.Rollout.ICodexRolloutTranslator<TMessageFormat>
        {
            public TMessageFormat Translate(Coven.Spellcasting.Agents.Codex.Rollout.CodexRolloutLine line)
                => throw new NotImplementedException("Register a real ICodexRolloutTranslator<T> in tests before Build().");
        }

    public IServiceProvider Services
        => _provider ?? throw new InvalidOperationException("Call Build() before accessing Services.");

    public ICovenAgent<TMessageFormat> GetAgent()
        => Services.GetRequiredService<ICovenAgent<TMessageFormat>>();

    public void Dispose()
    {
        _provider?.Dispose();
        _provider = null;
        if (!string.IsNullOrEmpty(_tempWorkspace)) { try { Directory.Delete(_tempWorkspace, true); } catch { } }
    }
}