namespace Coven.Spellcasting.Agents.Validation;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Helper base for building idempotent validators with a spec-hash stamp file and optional re-probe.
/// Keeps the public surface small; override the protected hooks as needed.
/// </summary>
public abstract class IdempotentAgentValidation : IAgentValidation
{
    protected IdempotentAgentValidation(string agentId)
    {
        AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
    }

    public string AgentId { get; }

    public async Task<AgentValidationResult> ValidateAsync(
        Spellcasting.Agents.SpellContext? context = null,
        CancellationToken ct = default)
    {
        // If a fast probe confirms readiness, persist the stamp (best-effort) and return Noop.
        if (await IsAlreadyReadyAsync(context, ct).ConfigureAwait(false))
        {
            await TryWriteStampAsync(context, ct).ConfigureAwait(false);
            return AgentValidationResult.Noop("Already ready (probe)");
        }

        var specHash = ComputeSpecHash(context);

        // If stamp matches, no work needed.
        if (await StampMatchesAsync(specHash, ct).ConfigureAwait(false))
        {
            return AgentValidationResult.Noop("Already ready (stamp)");
        }

        // If not permitted to run commands, skip provisioning.
        var perms = context?.Permissions;
        if (perms is null || !perms.Allows<Spellcasting.Agents.RunCommand>())
        {
            return AgentValidationResult.Skipped("Insufficient permissions: RunCommand not granted");
        }

        // Perform provisioning; allow derived classes to do what they need.
        await ProvisionAsync(context, ct).ConfigureAwait(false);

        // Persist the new stamp.
        await WriteStampAsync(specHash, ct).ConfigureAwait(false);
        return AgentValidationResult.Performed();
    }

    /// <summary>
    /// Derived classes implement the actual provisioning (install/check/fix) here.
    /// Guaranteed to run only when permissions allow and stamp/probe indicate work needed.
    /// </summary>
    protected abstract Task ProvisionAsync(Spellcasting.Agents.SpellContext? context, CancellationToken ct);

    /// <summary>
    /// Optional fast check to avoid work even if stamp is missing or stale (e.g., check CLI presence/version).
    /// Default: false (no fast probe).
    /// </summary>
    protected virtual Task<bool> IsAlreadyReadyAsync(Spellcasting.Agents.SpellContext? context, CancellationToken ct)
        => Task.FromResult(false);

    /// <summary>
    /// Compute a stable spec string representing the desired environment (e.g., version/options).
    /// Default: agent id only; override to add version/options.
    /// </summary>
    protected virtual string ComputeSpec(Spellcasting.Agents.SpellContext? context) => AgentId;

    /// <summary>
    /// Location for stamp files. Default: LocalApplicationData/Coven/agents/{AgentId}
    /// </summary>
    protected virtual string GetStampDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "Coven", "agents", AgentId);
    }

    private sealed class StampModel
    {
        public string SpecHash { get; set; } = string.Empty;
        public DateTimeOffset WrittenAt { get; set; }
    }

    private string ComputeSpecHash(Spellcasting.Agents.SpellContext? context)
    {
        var spec = ComputeSpec(context) ?? string.Empty;
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(spec);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private string GetStampPath() => Path.Combine(GetStampDirectory(), "stamp.json");

    private async Task<bool> StampMatchesAsync(string specHash, CancellationToken ct)
    {
        try
        {
            var path = GetStampPath();
            if (!File.Exists(path)) return false;
            await using var fs = File.OpenRead(path);
            var model = await JsonSerializer.DeserializeAsync<StampModel>(fs, cancellationToken: ct).ConfigureAwait(false);
            return model?.SpecHash == specHash;
        }
        catch
        {
            return false;
        }
    }

    private async Task TryWriteStampAsync(Spellcasting.Agents.SpellContext? context, CancellationToken ct)
    {
        try
        {
            var specHash = ComputeSpecHash(context);
            await WriteStampAsync(specHash, ct).ConfigureAwait(false);
        }
        catch
        {
            // best-effort only
        }
    }

    private async Task WriteStampAsync(string specHash, CancellationToken ct)
    {
        var dir = GetStampDirectory();
        Directory.CreateDirectory(dir);
        var path = GetStampPath();
        var model = new StampModel { SpecHash = specHash, WrittenAt = DateTimeOffset.UtcNow };
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }
}

