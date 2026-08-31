// SPDX-License-Identifier: BUSL-1.1

using Coven.Ui.Desktop.Logging;
using LLama.Abstractions;
using LLama.Native;

namespace Coven.Ui.Desktop.Local;

/// <summary>
/// Chooses the LLamaSharp native backend at runtime.
/// </summary>
/// <remarks>
/// <para>
/// The application references both the CPU and CUDA 12 backend packages. LLamaSharp resolves
/// its native library on first use, so the preference has to be registered <b>before</b> any
/// call into <c>NativeApi</c> — after that <see cref="NativeLibraryConfig.LibraryHasLoaded"/>
/// is true and configuration is frozen.
/// </para>
/// <para>
/// Configuration is deferred until a local session is actually built rather than run at
/// startup: probing native libraries costs time and touches GPU drivers, and a user who only
/// ever talks to a hosted provider should not pay for it. Nothing else in the application
/// calls into LLamaSharp, so by the time this runs the library is still unloaded.
/// </para>
/// </remarks>
internal static class LocalBackend
{
    private static readonly Lock _gate = new();
    private static string? _description;

    /// <summary>
    /// Registers the backend preference once and reports which library was selected.
    /// </summary>
    /// <returns>
    /// A short human-readable description of the selected backend, or a note explaining why
    /// selection could not be determined. Never throws — a probe failure must not stop a
    /// session from starting, because the real load attempt produces a far better error.
    /// </returns>
    public static string EnsureConfigured()
    {
        lock (_gate)
        {
            if (_description is not null)
            {
                return _description;
            }

            _description = Configure();
            return _description;
        }
    }

    private static string Configure()
    {
        try
        {
            // LibraryHasLoaded is per-library state, reached through the LLama config instance.
            if (NativeLibraryConfig.LLama.LibraryHasLoaded)
            {
                // Something already forced a load; the preference below would be ignored, so
                // say so rather than implying we chose it.
                return "already loaded (backend not selected by this application)";
            }

            // Prefer CUDA, but allow the fallback chain so a machine without a usable GPU
            // still gets a working CPU backend instead of a hard failure.
            //
            // The log callback matters as much as the backend choice. llama.cpp explains its
            // own failures — "unknown model architecture", tensor mismatches, allocation
            // failures — on its native stdout, which a WinExe discards. Without this, a model
            // that cannot load reports only "Failed to load model '<path>'", which names the
            // file and nothing about why.
            NativeLibraryConfig.All
                .WithCuda(true)
                .WithAutoFallback(true)
                .WithLogCallback(new NativeErrorCapture(
                    new FileLoggerProvider(AppLog.FilePath).CreateLogger("llama.cpp")));

            return Probe();
        }
        catch (Exception ex)
        {
            return $"selection failed ({ex.GetType().Name}); LLamaSharp will pick a default";
        }
    }

    /// <summary>
    /// Asks LLamaSharp what it would load without committing to it. Per the SDK, a dry run
    /// leaves the configuration mutable, so this is safe to call before the real load.
    /// </summary>
    private static string Probe()
    {
        try
        {
            if (!NativeLibraryConfig.LLama.DryRun(out INativeLibrary? library) || library is null)
            {
                return "no compatible native library found";
            }

            NativeLibraryMetadata? metadata = library.Metadata;
            if (metadata is null)
            {
                return "selected (details unavailable)";
            }

            string accelerator = metadata.UseCuda ? "CUDA" : metadata.UseVulkan ? "Vulkan" : "CPU";
            return $"{accelerator} ({metadata.AvxLevel})";
        }
        catch (Exception ex)
        {
            return $"probe failed ({ex.GetType().Name})";
        }
    }
}
