using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coven.Core;

namespace Coven.Core.Routing;

internal sealed class RegisteredBlock
{
    public required MagikBlockDescriptor Descriptor { get; init; }
    public required int RegistryIndex { get; init; }
    public required Func<object, Task<object>> Invoke { get; init; }
    public required Func<Board, IOrchestratorSink, string?, object, Task> InvokePull { get; set; }
    public required Type InputType { get; init; }
    public required Type OutputType { get; init; }
    public required string BlockTypeName { get; init; }
    public required ISet<string> Capabilities { get; init; }
    public IReadOnlyList<string> ForwardNextTags { get; set; } = Array.Empty<string>();
}
