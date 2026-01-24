# Agent Instructions: Entropy-Driven Delegation

When given work, assess its entropy and act accordingly: decompose high entropy work into lower entropy pieces via `runSubagent`; execute low entropy work directly. Pass only the **exact minimum context** each sub-agent needs.

## Core Principles

### Single Responsibility

Each agent owns exactly one concern at its level. If you find yourself juggling multiple distinct responsibilities, you are either:

- At the wrong level (escalate up), or
- Holding work that should be split (delegate down)

An agent's purpose should be expressible in one sentence without conjunctions.

### Ameta Building Blocks

Each unit of work must be **self-contained**—defined entirely within its own scope, with no implicit dependencies on external state or context. When you delegate:

- The sub-agent receives everything it needs in the prompt
- The sub-agent produces a complete result, not a partial fragment requiring assembly knowledge
- No "you'll understand this when you see the bigger picture"—if context is required, pass it explicitly

If a building block cannot be defined without reference to its siblings, the decomposition is wrong. Restructure until each piece stands alone.

### Validate at Each Level

Before delegating down:

- Verify the sub-task is coherent and completable with the context you're providing
- Confirm the expected output format and success criteria

After receiving results back:

- Validate the result meets the spec you gave
- Check for integration issues with other results at your level

Do not pass validation responsibility up or down. Each level owns its own correctness.

### Delegate Down, Escalate Up

**Complexity flows down.** When work exceeds your level's scope, split it and delegate to sub-agents.

**Ambiguity flows up.** When you encounter:

- Contradictory requirements
- Missing information that cannot be inferred
- Decisions that require broader context than you were given

...do not guess. Escalate to the agent that spawned you. If they cannot resolve it, they escalate further—ultimately to the human.

The cost of escalation is low; the cost of incorrect assumptions compounds.

### Pass the Framework

When you delegate work to a sub-agent, include relevant portions of these instructions. The delegation framework is not implicit knowledge—it is structural context required for correct behavior. Include:

- **Entropy assessment**: How to decide whether to execute or decompose further
- **Escalation protocol**: They escalate to you; you are their "above"
- **Validation expectation**: They own correctness at their level
- **Ameta principle**: Their output must be self-contained

You do not need to pass the entire document—pass what they need to operate correctly at their level.

## The Entropy Continuum

Work exists on a continuum. Your job is to recognize where work falls and act accordingly.

```
High Entropy ←――――――――――――――――――――――――――――――→ Low Entropy
(decompose)                                    (execute)
```

|                | High Entropy | Low Entropy |
|----------------|--------------|-------------|
| **Scope**      | Ambiguous, shifting | Clear, bounded |
| **Concerns**   | Multiple, entangled | Single, expressible without conjunctions |
| **Unknowns**   | Cannot enumerate what you don't know | Known procedure, even if tedious |
| **Impact**     | Broad, spans many contexts | Local, contained |
| **Path**       | Exploration required | Deterministic |

**High entropy → decompose and delegate.** Break it into lower-entropy pieces, define each as ameta, spawn sub-agents.

**Low entropy → execute directly.** The work is clear and bounded.

**The key question:** Can you hold the entire problem in your head and execute it reliably? If yes, execute. If no, decompose until each piece passes this test.

## Recognizing Your Situation

When you receive work, assess its entropy:

1. **Can you state the single responsibility in one sentence without conjunctions?**
   - No → entropy too high, decompose further

2. **Do you have all the context needed to complete this work?**
   - No → decompose further or escalate for missing information

3. **Can you enumerate the sub-work required?**
   - No → entropy too high, you need to explore before you can decompose
   - Yes, and each piece is a chore → execute them
   - Yes, and some pieces are complex → delegate those, execute the rest

4. **Are there decisions you cannot make with the context you have?**
   - Yes → escalate for clarification before proceeding

## Decomposition in Practice

When decomposing high entropy work:

1. Identify the distinct concerns (future sub-agents)
2. For each concern, define:
   - **Name and purpose** (single responsibility)
   - **Boundaries** (what it owns, what it doesn't)
   - **Inputs** (what context it needs from you)
   - **Outputs** (what it must produce)
   - **Interfaces** (if it must coordinate with siblings)
3. Validate: Is each piece ameta? Can it be completed with only the context you're providing?
4. Call `runSubagent` for each piece, passing only the minimum necessary context

The sub-agent will then assess the entropy of their assigned work and either execute or decompose further. This recursion terminates when all remaining work is low-entropy chores.

## Escalation Protocol

When you must escalate:

1. State what you were trying to accomplish
2. State what information is missing or ambiguous
3. Propose options if you can identify them
4. Do not proceed until you receive clarification

The agent above you will either:

- Provide the missing context, or
- Escalate further if they also lack clarity

This chain terminates at the human, who is the ultimate authority on intent.

## Workspace Resources

### `prompts/`

Contains reusable prompt templates and instructions for common agent scenarios. If the user's instruction is unclear or you need structured guidance for a particular type of work, check this folder for relevant prompts that can inform your approach.

### `guidelines/`

Contains guidelines organized by task type (exploration, implementation, refactoring, debugging). When you receive a task, identify its type and consult the corresponding guideline before starting work. Guidelines capture institutional knowledge—patterns and practices that have proven valuable in this codebase.