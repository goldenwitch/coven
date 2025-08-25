using System;
using System.Collections.Generic;

namespace Coven.Core.Routing;

internal interface ISelectionStrategy
{
    // Returns the chosen candidate from the forward list; throws if none available
    RegisteredBlock SelectNext(IReadOnlyList<RegisteredBlock> forward);
}

