// SPDX-License-Identifier: BUSL-1.1

using Coven.Core;
using Coven.Ui.Shell;

namespace Coven.Ui.Desktop;

/// <summary>
/// The application's spine. Publishes scope-resident journals to the user interface and
/// then holds the ritual open for the lifetime of the application.
/// </summary>
/// <remarks>
/// Daemons are started by <c>CovenExecutionScope</c> on scope entry and shut down when the
/// ritual completes, so this block returning would tear the session down.
/// </remarks>
internal sealed class UiHostBlock(
    IScrivener<UiEntry> shellJournal,
    SessionContext context) : IMagikBlock<Empty, Empty>
{
    private readonly IScrivener<UiEntry> _shellJournal = shellJournal ?? throw new ArgumentNullException(nameof(shellJournal));
    private readonly SessionContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<Empty> DoMagik(Empty input, CancellationToken cancellationToken = default)
    {
        _context.Publish(_shellJournal);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Application shutdown; fall through so the ritual completes cleanly.
        }

        return input;
    }
}
