# Coven.Agents.LLamaSharp

Local LLM agent integration via [LLamaSharp](https://github.com/SciSharp/LLamaSharp). Loads GGUF models in-process using llama.cpp, with no external server required.

## Quick Start

```csharp
using Coven.Agents.LLamaSharp;

LLamaSharpClientConfig cfg = new()
{
    ModelPath = Environment.GetEnvironmentVariable("LLAMASHARP_MODEL_PATH")!,
    SystemPrompt = "You are a helpful assistant."
};

services.AddLLamaSharpAgents(cfg, registration =>
{
    registration.EnableStreaming(); // optional
});
```

## Prerequisites

- A GGUF model file (e.g., from [Hugging Face](https://huggingface.co/))
- A LLamaSharp backend NuGet package matching your hardware:
  - `LLamaSharp.Backend.Cpu` — CPU-only
  - `LLamaSharp.Backend.Cuda12` — NVIDIA GPU (CUDA 12)
  - `LLamaSharp.Backend.Vulkan` — Vulkan GPU

Install the backend package in your application project (not in this library).

## Features

- **In-process inference**: No HTTP server, no sidecar. Model loads directly into the application.
- Journals: `IScrivener<AgentEntry>`, `IScrivener<LLamaSharpEntry>` (keyed internal scrivener).
- Gateway: `StatelessExecutor` from LLamaSharp; each call builds a full prompt from the journal.
- Transmuters: `LLamaSharpTransmuter` (LLamaSharp↔Agent), `LLamaSharpTranscriptBuilder` (journal→prompt).
- Windowing: default agent chunk policies when streaming is enabled.
- Daemons: `LLamaSharpAgentDaemon` + windowing daemons (when streaming).

## Configuration

| Property | Required | Description |
|----------|----------|-------------|
| `ModelPath` | ✅ | Absolute path to the GGUF model file |
| `ModelName` | | Display name (defaults to filename) |
| `GpuLayerCount` | | Layers offloaded to GPU (0 = CPU, -1 = all) |
| `ContextSize` | | Context window size in tokens (default 2048) |
| `Temperature` | | Sampling temperature |
| `TopP` | | Nucleus sampling parameter |
| `MaxTokens` | | Maximum tokens to generate (default 256) |
| `SystemPrompt` | | System instruction prepended to conversations |
| `HistoryClip` | | Max transcript turns to include |
| `Threads` | | CPU thread count (null = LLamaSharp default) |

## Entry Types

| Entry Type | Direction | Description |
|------------|-----------|-------------|
| `LLamaSharpEfferent` | Outgoing | User message to the model |
| `LLamaSharpAfferent` | Incoming | Complete response from the model |
| `LLamaSharpAfferentChunk` | Incoming | Streaming text chunk (draft) |
| `LLamaSharpAck` | Internal | Synchronization acknowledgement |
| `LLamaSharpStreamCompleted` | Incoming | Marks end of streaming response |

## Notes

- Uses `StatelessExecutor` so the journal remains the sole source of truth. Each inference call reconstructs the full prompt from the journal via `LLamaSharpTranscriptBuilder`.
- Model loading happens once in `ConnectAsync()` and is reused across all inference calls.
- The backend NuGet package must be installed in the final executable project, not in this library.

## See Also

- Branch: `Coven.Agents`.
- Architecture: Abstractions and Branches; Windowing and Shattering.
