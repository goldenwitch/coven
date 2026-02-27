# Coven.Toys.ConsoleLLamaSharp

A minimal console chat application using a local GGUF model via LLamaSharp.

## Prerequisites

- .NET 10 SDK
- A GGUF model file (e.g., from [Hugging Face](https://huggingface.co/))
- A LLamaSharp backend package installed (e.g., `LLamaSharp.Backend.Cpu`)
- If using GPU acceleration with `LLamaSharp.Backend.Cuda12`, have a CUDA 12.x toolkit (version 13 is currently not compatible)

## Configuration

Set environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `LLAMASHARP_MODEL_PATH` | Absolute path to the GGUF model file | (required) |
| `LLAMASHARP_GPU_LAYERS` | GPU layers to offload (0 = CPU, -1 = all) | `0` |
| `LLAMASHARP_CONTEXT_SIZE` | Context window size in tokens | `2048` |
| `LLAMASHARP_BACKEND` | Set to `cuda` to use CUDA backend; otherwise auto-detect | (auto) |

## Running

```bash
# Install a backend package first
dotnet add package LLamaSharp.Backend.Cpu

# Set the model path and run
export LLAMASHARP_MODEL_PATH="/path/to/model.gguf"
dotnet run
```

## Features

- Console-based chat with a local LLM
- Declarative covenant routing
- No external API keys or network access required
