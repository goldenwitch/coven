// SPDX-License-Identifier: BUSL-1.1

using System.Text;

namespace Coven.Toys.MockProcess;

internal static class StdInLineReader
{
    public static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken = default)
    {
        // Read using cancel-aware StreamReader API and build a line manually.
        var sb = new StringBuilder();
        char[] one = new char[1];
        while (true)
        {
            int n = await reader.ReadAsync(one.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                // EOF
                if (sb.Length == 0) return null;
                return sb.ToString();
            }
            char ch = one[0];
            if (ch == '\r') continue; // handle CRLF
            if (ch == '\n') return sb.ToString();
            sb.Append(ch);
        }
    }
}

