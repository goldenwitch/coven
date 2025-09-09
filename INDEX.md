# Code

Project overview: see [README](/README.md).

## Coven Engine (Coven.Core)
- [Coven.Core](/src/Coven.Core/)
- [Coven.Core.Tests](/src/Coven.Core.Tests/)

## Coven with Agents (Coven.Spellcasting)
- [Coven.Spellcasting](/src/Coven.Spellcasting/)
- [Coven.Spellcasting.Agents](/src/Coven.Spellcasting.Agents/)
- [Coven.Spellcasting.Agents.Tests](/src/Coven.Spellcasting.Agents.Tests/)
- [Coven.Spellcasting.Agents.Validation](/src/Coven.Spellcasting.Agents.Validation/)
- [Coven.Spellcasting.Grimoire](/src/Coven.Spellcasting.Grimoire/)
- [Coven.Spellcasting.Tests](/src/Coven.Spellcasting.Tests/)

### Agent implementations
- [Coven.Spellcasting.Agents.Codex](/src/Coven.Spellcasting.Agents.Codex/)
- [Coven.Spellcasting.Agents.Codex.McpShim](/src/Coven.Spellcasting.Agents.Codex.McpShim/)

### Agent implementation tests
- [Coven.Spellcasting.Agents.Codex.Tests](/src/Coven.Spellcasting.Agents.Codex.Tests/)

## Coven Infrastructure for Chat
- [Coven.Chat](/src/Coven.Chat/)
- [Coven.Chat.Tests](/src/Coven.Chat.Tests/)

## Analyzers
- [Coven.Analyzers](/src/Coven.Analyzers/)
- [Coven.Analyzers.CodeFixes](/src/Coven.Analyzers.CodeFixes/)
- [Coven.Analyzers.Tests](/src/Coven.Analyzers.Tests/)

## Durables
- [Coven.Durables](/src/Coven.Durables/)
- [Coven.Durables.Tests](/src/Coven.Durables.Tests/)

## Sophia
- [Coven.Sophia](/src/Coven.Sophia/)
- [Coven.Sophia.Tests](/src/Coven.Sophia.Tests/)

## Samples
- [01.LocalCodexCLI](/samples/01.LocalCodexCLI/)
- [01.LocalCodexCLI.Tests](/samples/01.LocalCodexCLI.Tests/)

## Toys
- [Coven.Toys.ConsoleEcho](/Toys/Coven.Toys.ConsoleEcho/)
- [Coven.Toys.ConsoleAgentChat](/Toys/Coven.Toys.ConsoleAgentChat/)

# Architecture Guide

Start with Overview, then explore by topic. All paths below are under `/Architecture`.

- [Architecture README](/Architecture/README.md)

## Getting Started
- [Overview](/Architecture/Overview.md)
- [End-to-End Example](/Architecture/EndToEndExample.md)

## Core Concepts
- [MagikBlocks & Builder](/Architecture/MagikBlocks.md)
- [Tags & Routing](/Architecture/TagsAndRouting.md)
- [MagikTrick (Fenced Routing)](/Architecture/MagikTrick.md)

## Runtime
- [Board (Push & Pull)](/Architecture/Board.md)
- [Dependency Injection](/Architecture/DependencyInjection.md)
- [Connectivity](/Architecture/Connectivity.md)

## Spellcasting
- [Spellcasting (Design)](/Architecture/Spellcasting/Spellcasting.md)
- [Spellcasting.Spells](/Architecture/Spellcasting/Spellcasting.Spells.md)
- [Spellcasting.Agents](/Architecture/Spellcasting/Spellcasting.Agents.md)
- [Spellcasting.Agents.Validation](/Architecture/Spellcasting/Spellcasting.Agents.Validation.md)

## Chat Subsystem
- [Chat (Overview)](/Architecture/Chat/Chat.md)

## Tooling
- [Roslyn Analyzer Pack](/Architecture/Analyzer/Analyzer.md)

## Meta
- [Contributing](/Architecture/Contributing.md)
- [Licensing](/Architecture/Licensing.md)
