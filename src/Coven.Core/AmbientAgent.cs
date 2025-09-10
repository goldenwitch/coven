using Coven.Core.Di;

namespace Coven.Core;

// Ambient operations over "the" current agent, resolved from the ritual DI scope.
// Spell libraries can call these without knowing concrete agent types.
public static class AmbientAgent
{
    public interface IAgentEnvironment
    {
        Task CancelAsync(IServiceProvider? sp);
    }

    private static IAgentEnvironment? _env;
    public static void Configure(IAgentEnvironment environment)
    {
        _env = environment;
    }

    public static Task CancelAsync()
    {
        var env = _env;
        if (env is null) return Task.CompletedTask;
        return env.CancelAsync(CovenExecutionScope.CurrentProvider);
    }
}
