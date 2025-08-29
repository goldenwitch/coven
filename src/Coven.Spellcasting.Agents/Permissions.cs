namespace Coven.Spellcasting.Agents;

using System;
using System.Collections.Generic;

public interface ISpellAction { }

public sealed class WriteFile : ISpellAction { }
public sealed class RunCommand : ISpellAction { }
public sealed class NetworkAccess : ISpellAction { }

public sealed class AgentPermissions
{
    private readonly HashSet<Type> _grants = new();

    public AgentPermissions Grant<TAction>() where TAction : ISpellAction
    { _grants.Add(typeof(TAction)); return this; }

    public bool Allows<TAction>() where TAction : ISpellAction
        => _grants.Contains(typeof(TAction));

    public static AgentPermissions None()     => new();
    public static AgentPermissions AutoEdit() => new AgentPermissions().Grant<WriteFile>();
    public static AgentPermissions FullAuto() => new AgentPermissions().Grant<WriteFile>().Grant<RunCommand>();
}

