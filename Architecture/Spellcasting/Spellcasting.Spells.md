# Spells

Spells represent a tool call that the agent can intentionally invoke.
They are defined as well typed dotnet classes and functions, but with some added flavor.

## Spell Registration
Because spells need to be able to be cast from outside of a C# context, we automatically generate:
1. Json schema for the input type.
2. Json schema for the output type.
3. A unique spellid.

This generation happens during Coven finalization (aka .Done()).

## DI
Spells will always be DI compatible. When we invoke the spell, we ensure that it is created from the DI container with all of it's dependencies intact.

## Agent wireup
If you are leveraging an agent to utilize these spells, the agent needs to know how to call the tools.

We support two tool calling paths:
1. MCP
2. Direct

### MCP
For codex cli or other MCP aware agents, we wrap spells in a MCP Host that we scope to the agents executing block.

### Direct
For agents that support emitting direct tool calls, we map these tool calls to the registered spells.

## Spellbooks
A Spellbook represents the set of spells that land in the MagikUser.

It contains:
1. The list of spells.
2. The schema for the spells.
3. Agent guidance around how and when to use the spells.

By default the spellbook passed into a MagikUser is automatically built from:
1. All valid spells in assembly.
2. How and when documentation comes from attributes and/or xml documentation.