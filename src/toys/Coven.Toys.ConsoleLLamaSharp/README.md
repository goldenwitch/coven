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
| `LLAMASHARP_BACKEND` | Set to `cuda` to use CUDA backend; otherwise auto-detect | (auto) || `LLAMASHARP_RESPONSE_MARKER` | Marker separating thinking output from the response (for reasoning models) | (none) |
## Running

```bash
# Set the model path and run (CPU backend is included by default)
export LLAMASHARP_MODEL_PATH="/path/to/model.gguf"
dotnet run

# For NVIDIA GPU acceleration, swap the backend package:
# dotnet remove package LLamaSharp.Backend.Cpu
# dotnet add package LLamaSharp.Backend.Cuda12
# export LLAMASHARP_BACKEND=cuda
# export LLAMASHARP_GPU_LAYERS=-1
```

## Features

- Console-based chat with a local LLM
- Declarative covenant routing
- No external API keys or network access required
