# Coven.Gemini.Client

Lightweight REST client for Google Gemini API. Provides wire types and HTTP communication isolated from the agent integration layer.

## Features

- Wire types: JSON-serializable DTOs for Gemini generateContent / streamGenerateContent.
- GeminiRestClient: thin wrapper over HttpClient for sync and streaming calls.
- Zero external SDK dependencies: uses System.Text.Json and System.Net.Http directly.

## Usage

```csharp
using Coven.Gemini.Client;

GeminiRestClientOptions options = new()
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")!,
    Model = "gemini-2.0-flash"
};

using GeminiRestClient client = new(options);

GeminiGenerateContentRequest request = new()
{
    Contents = [new GeminiContent { Role = "user", Parts = [new GeminiPart { Text = "Hello!" }] }]
};

GeminiGenerateContentResponse response = await client.GenerateContentAsync(request);
string text = response.GetText();
```

## Streaming

```csharp
await foreach (GeminiGenerateContentResponse chunk in client.StreamGenerateContentAsync(request))
{
    Console.Write(chunk.GetText());
}
```

## Notes

- The client targets the v1beta REST API at `generativelanguage.googleapis.com`.
- API key is passed as a query parameter per Gemini conventions.
- Schema may drift as Gemini evolves; this client is updated independently from `Coven.Agents.Gemini`.
