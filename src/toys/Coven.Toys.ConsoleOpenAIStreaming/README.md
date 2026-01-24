# Coven.Toys.ConsoleOpenAIStreaming

Console chat with OpenAI streaming responses. Demonstrates windowing policies for chunked output and thought streams.

## Prerequisites

- OpenAI API key (set in `Program.cs` or via `OPENAI_API_KEY` environment variable)

## How to Run

```bash
dotnet run
```

Type messages in the console; responses stream in real-time with configured windowing policies for both output and thought chunks.
