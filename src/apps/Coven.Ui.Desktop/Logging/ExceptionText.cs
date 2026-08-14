// SPDX-License-Identifier: BUSL-1.1

using System.Text;

namespace Coven.Ui.Desktop.Logging;

/// <summary>
/// Renders an exception chain as the text a user is shown.
/// </summary>
/// <remarks>
/// Reporting only <c>ex.Message</c> is close to useless here, because the exceptions that
/// reach the UI are usually wrappers: a failed model load arrives as
/// <c>DaemonStartupException("Scope activation failed: daemon startup error")</c>, whose own
/// message says nothing about what went wrong. The cause is the innermost exception, so the
/// whole chain is reported with duplicates collapsed.
/// </remarks>
internal static class ExceptionText
{
    /// <summary>
    /// Describes an exception as a single line per distinct cause, outermost first.
    /// </summary>
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        List<string> lines = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            // An AggregateException's own message just counts its inner exceptions.
            if (current is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.Flatten().InnerExceptions)
                {
                    Add(lines, seen, inner);
                }

                continue;
            }

            Add(lines, seen, current);
        }

        return lines.Count == 0 ? error.GetType().Name : string.Join(Environment.NewLine, lines);
    }

    private static void Add(List<string> lines, HashSet<string> seen, Exception error)
    {
        string message = error.Message;
        if (string.IsNullOrWhiteSpace(message) || !seen.Add(message))
        {
            return;
        }

        lines.Add(message);
    }
}
