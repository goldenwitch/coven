# Code

Project overview: see [README](/README.md).

## Coven Engine (Coven.Core)
- [Coven.Core](/src/Coven.Core/)
- [Coven.Core.Tests](/src/Coven.Core.Tests/)

## Coven with Agents (Coven.Spellcasting)
- [Coven.Spellcasting](/src/Coven.Spellcasting/)
- [Coven.Spellcasting.Tests](/src/Coven.Spellcasting.Tests/)
- [Coven.Spellcasting.Agents](/src/Coven.Spellcasting.Agents/)
- [Coven.Spellcasting.Agents.Validation](/src/Coven.Spellcasting.Agents.Validation/)
- [Coven.Spellcasting.Agents.Tests](/src/Coven.Spellcasting.Agents.Tests/)

### Agent implementations
- [Coven.Spellcasting.Agents.Codex](/src/Coven.Spellcasting.Agents.Codex/)

## Coven Infrastructure for Chat
- [Coven.Chat](/src/Coven.Chat/)
- [Coven.Chat.Journal](/src/Coven.Chat.Journal/)
- [Coven.Chat.Tests](/src/Coven.Chat.Tests/)
- [Coven.Chat.Adapter](/src/Coven.Chat.Adapter/)
- [Coven.Chat.Adapter.Tests](/src/Coven.Chat.Adapter.Tests/)

### Chat integration implementations
- [Coven.Chat.Adapter.Discord](/src/Coven.Chat.Adapter.Discord/)
- [Coven.Chat.Adapter.Discord.Tests](/src/Coven.Chat.Adapter.Discord.Tests/)

## Analyzers
- [Coven.Analyzers](/src/Coven.Analyzers/)
- [Coven.Analyzers.CodeFixes](/src/Coven.Analyzers.CodeFixes/)
- [Coven.Analyzers.Tests](/src/Coven.Analyzers.Tests/)

# Architecture Guide

Start with Overview, then explore by topic. This index includes all design docs in this folder and subfolders.

## Getting Started
- [Overview](./Overview.md)
- [Project Scope](./ProjectScope.md)
- [End-to-End Example](./EndToEndExample.md)

## Core Concepts
- [MagikBlocks & Builder](./MagikBlocks.md)
- [Tags & Routing](./TagsAndRouting.md)
- [MagikTrick (Fenced Routing)](./MagikTrick.md)

## Runtime
- [Board (Push & Pull)](./Board.md)
- [Pull Mode Internals](./PullMode.md)
- [Dependency Injection](./DependencyInjection.md)
- [Connectivity](./Connectivity.md)

## Spellcasting
- [Spellcasting (Design)](./Spellcasting/Spellcasting.md)
- [Spellcasting.Agents](./Spellcasting/Spellcasting.Agents.md)
- [Spellcasting.Agents.Validation](./Spellcasting/Spellcasting.Agents.Validation.md)

## Chat Subsystem
- [Chat (Overview)](./Chat/Chat.md)
- [Chat Journal](./Chat/Chat.Journal.md)
- [Chat Journal Compaction](./Chat/Chat.Journal.Compaction.md)
- [Discord Adapter](./Chat/Chat.Adapter.Discord.md)

## Tooling
- [Roslyn Analyzer Pack](./Analyzer/Analyzer.md)

## Meta
- [Contributing](./Contributing.md)
- [Licensing](./Licensing.md)
