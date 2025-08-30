## Getting Started
- [Overview](./Overview.md) (this document)
- [End-to-End Example](./EndToEndExample.md)

# Coven

Coven is an engine for orchestrating multiple AI coding agents to collaborate on complex tasks.

Coven coordinates agents to operate in a shared context with clear roles, policies, and handoffs. It builds on Codex CLI’s sandboxing and tooling, adding multi‑agent workflows, and traditional compute for coordination. Technically, you could wrap any tool with it, but we are targeting a mix of LLMs and traditional programming.

## Project Scope

- Run on one box. A distributed version of this might be offered in the future, but for now just make calls out to the network in a MagikBlock if you need to do remote stuff.
- A minimal set of samples that satisfy the needs of the selfish developer writing these docs while reluctantly avoiding avoiding work.
- A library of extensions to the core orchestration engine that makes it easy to orchestrate AI Agents.
- Initially, just dotnet. Something something something money something something something typescript.
