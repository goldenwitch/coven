using Coven.Core;

namespace Coven.Spellcasting.Agents;

// Default environment that resolves IAgentControl from DI and performs actions.
internal sealed class DefaultAgentEnvironment : AmbientAgent.IAgentEnvironment
{
    public async Task CancelAsync(IServiceProvider? sp)
    {
        if (sp is null) return;
        var ctrl = sp.GetService(typeof(IAgentControl)) as IAgentControl;
        if (ctrl is null) return;
        try { await ctrl.CloseAgent().ConfigureAwait(false); } catch { }
    }
}

