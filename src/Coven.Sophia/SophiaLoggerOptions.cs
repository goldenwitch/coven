using Microsoft.Extensions.Logging;

namespace Coven.Sophia;

public sealed class SophiaLoggerOptions
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public string? Label { get; set; }
    public bool IncludeScopes { get; set; } = true;
    // When enabled, entries from breadcrumb-related categories are emitted in a compact form (message + scopes only)
    public bool CompactBreadcrumbs { get; set; } = true;
    // Categories treated as breadcrumbs; prefix match is used
    public string[] BreadcrumbCategories { get; set; } = new[]
    {
        "Coven.Ritual",
        "Coven.Chat.Adapter.SimpleAdapterHost"
    };
}
