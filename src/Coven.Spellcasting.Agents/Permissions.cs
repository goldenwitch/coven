namespace Coven.Spellcasting.Agents;

using System;
using System.Collections.Generic;

public sealed class WriteFile : ISpellPermission { }
public sealed class RunCommand : ISpellPermission { }
public sealed class NetworkAccess : ISpellPermission { }

public sealed class AgentPermissions
{
    private readonly HashSet<Type> _grants = new();

    public AgentPermissions Grant<TAction>() where TAction : ISpellPermission
    { _grants.Add(typeof(TAction)); return this; }

    public bool Allows<TAction>() where TAction : ISpellPermission
        => _grants.Contains(typeof(TAction));

    public static AgentPermissions None()     => new();
    public static AgentPermissions AutoEdit() => new AgentPermissions().Grant<WriteFile>();
    public static AgentPermissions FullAuto() => new AgentPermissions().Grant<WriteFile>().Grant<RunCommand>();
}

