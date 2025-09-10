Sophia Logging

- Register storage: `services.AddSingleton<IDurableList<string>>(_ => new SimpleFileStorage<string>("path/to/logs.json"));`
- Add provider: `services.AddSophiaLogging(o => { o.MinimumLevel = LogLevel.Information; o.Label = "coven"; });`
- Inject logger: add `ILogger<T>` to constructors; messages persist via `IDurableList<string>`.

Example

```
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Coven.Durables;
using Coven.Sophia;

var services = new ServiceCollection();
services.AddLogging(); // or rely on Host.CreateDefaultBuilder
services.AddSingleton<IDurableList<string>>(_ => new SimpleFileStorage<string>(Path.Combine(Path.GetTempPath(), "coven-logs.json")));
services.AddSophiaLogging(new SophiaLoggerOptions { MinimumLevel = LogLevel.Debug, Label = "coven" });

// Now any `ILogger<T>` can be injected and used.
```
