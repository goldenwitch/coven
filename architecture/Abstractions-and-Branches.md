# Abstractions and Branches:

Single takeaway: Integrate with Chat and Agents; swap leaves (Discord, Console, OpenAI) without changing your application logic.

## Spine, Branches, Leaves
- Spine: your ritual pipeline (MagikBlocks) orchestrating work.
- Branches: Chat and Agents abstractions that expose typed journals and services.
- Leaves: integrations translating abstractions to external systems (e.g., Discord, OpenAI).

Your block logic writes/reads journal entries from Chat/Agents. The specific leaf is free to change (or multiply) behind the branch boundary.

![Diagram showing what a Coven "Branch" is. It shows a Spine Segment (where user code lives) connecting to a "branch" abstraction, isolating the user code from the integrations.](<../assets/Normal Looking Branch.svg>)

## Directionality
- Spine: your block/user code lives here.
- Efferent: spine → leaves (outbound from your code to adapters).
- Afferent: leaves → spine (inbound to your code from adapters).

## Chat
- Contract: `IScrivener<ChatEntry>` representing inbound (afferent) and outbound (efferent) chat entries.
- Examples: Discord and Console leaves both implement chat daemons and journals.
- Windowing: chat drafts/outputs can be controlled via windowing policies.

## Agents
- Contract: `IScrivener<AgentEntry>` representing prompts, thoughts, responses, and streaming chunks.
- Streaming: leaves can stream agent outputs incrementally; window policies decide emission.
- Templating: use `ITransmuter` to shape request/response items (context, persona, metadata).

## Swap Without Rewrites
- Replace Discord with Console by changing DI registration; keep Chat code unchanged.
- Switch AI providers or models by updating the Agents leaf; keep Agent journaling unchanged.
- Combine multiple leaves (e.g., Discord + Console) and route via the same branch journals.

## DI Patterns
- Register branches in DI via extension methods; prefer `TryAdd` within libraries.
- Start leaf daemons in a block and optionally wait for `Status.Running`.
- Keep application code unaware of leaf‑specific SDKs.

## Related
- Journaling/Scriveners for boundary decoupling.
- Windowing/Shattering for streaming behavior.
