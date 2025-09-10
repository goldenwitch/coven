using Microsoft.Extensions.Logging;

namespace Coven.Sophia;

public sealed class SophiaLoggerOptions
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public string? Label { get; set; }
    public bool IncludeScopes { get; set; } = true;
}
