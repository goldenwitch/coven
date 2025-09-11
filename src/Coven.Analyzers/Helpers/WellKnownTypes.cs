// SPDX-License-Identifier: BUSL-1.1

using Microsoft.CodeAnalysis;

namespace Coven.Analyzers.Helpers;

internal static class WellKnownTypes
{
    internal static INamedTypeSymbol? GetISelectionStrategy(Compilation compilation)
        => compilation.GetTypeByMetadataName("Coven.Core.Routing.ISelectionStrategy");
}
