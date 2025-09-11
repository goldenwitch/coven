// SPDX-License-Identifier: BUSL-1.1

using System.Collections.Generic;
using System.IO;

namespace Coven.Chat.Tests.TestTooling;

// Centralized factory helpers to reduce duplication across test files.
public static class ScrivenerFactory
{
    public static IEnumerable<object[]> Both<T>(string tempPrefix) where T : notnull
    {
        yield return new object[] { new Func<IScrivener<T>>(() => new InMemoryScrivener<T>()) };
        yield return new object[]
        {
            new Func<IScrivener<T>>(
                () => new Coven.Chat.FileScrivener<T>(
                    Path.Combine(Path.GetTempPath(), tempPrefix + System.Guid.NewGuid().ToString("N"))
                ))
        };
    }
}