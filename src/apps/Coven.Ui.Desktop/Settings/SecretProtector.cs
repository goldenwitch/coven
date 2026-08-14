// SPDX-License-Identifier: BUSL-1.1

using System.Security.Cryptography;
using System.Text;

namespace Coven.Ui.Desktop.Settings;

/// <summary>
/// Protects API keys at rest.
/// </summary>
/// <remarks>
/// <para>
/// On Windows, DPAPI encrypts values against the current user account. .NET exposes no
/// cross-platform equivalent, so on other platforms values are stored as plain text with the
/// file restricted to the owner.
/// </para>
/// <para>
/// The fallback is deliberately <b>labelled</b> rather than dressed up as encryption
/// (<see cref="IsEncrypted"/> drives a warning in the options window). Obfuscating with base64
/// and calling it protected would be worse than being plain about it — a user who knows the
/// key is readable can decide what that means for their machine.
/// </para>
/// </remarks>
internal static class SecretProtector
{
    private const string DpapiPrefix = "dpapi:";
    private const string PlainPrefix = "plain:";

    /// <summary>Whether stored secrets are actually encrypted on this platform.</summary>
    public static bool IsEncrypted => OperatingSystem.IsWindows();

    /// <summary>Encodes a secret for storage.</summary>
    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        if (!OperatingSystem.IsWindows())
        {
            return PlainPrefix + plaintext;
        }

        byte[] encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return DpapiPrefix + Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decodes a stored secret. Returns an empty string when the value cannot be read —
    /// a DPAPI blob copied from another machine or account will not decrypt, and losing a
    /// key is better handled by prompting for it again than by throwing at startup.
    /// </summary>
    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return string.Empty;
        }

        if (stored.StartsWith(PlainPrefix, StringComparison.Ordinal))
        {
            return stored[PlainPrefix.Length..];
        }

        if (!stored.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            // Written by an older build before prefixes existed.
            return stored;
        }

        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            byte[] decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(stored[DpapiPrefix.Length..]),
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
