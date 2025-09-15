// SPDX-License-Identifier: BUSL-1.1

using Coven.Spellcasting.Agents;

namespace Coven.Toys.RolloutMuxConsole;

internal static class SendPortExtensions
{
    public static async Task SafeWriteAsync(this ISendPort send, string data, CancellationToken token)
    {
        try { await send.WriteAsync(data, token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (FileNotFoundException fnf)
        {
            Console.Error.WriteLine($"[codex-missing] {fnf.Message}");
        }
        catch (DirectoryNotFoundException dnf)
        {
            Console.Error.WriteLine($"[workspace-invalid] {dnf.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[write-error] {ex.Message}");
        }
    }
}

