namespace Coven.Spellcasting.Agents.Codex.Tests.Infrastructure;

/// <summary>
/// Test fixture contract that supplies a concrete ITailMux and coordinates the backing medium
/// used for tailing. Implementations decide how to build the mux and how to stimulate incoming
/// data for tests without exposing implementation details to the test suite.
///
/// Responsibilities:
/// - CreateMux: construct a new mux instance and set up any per-instance resources (e.g., file path).
/// - CreateBackingFileAsync: if the mux tails a file, create it on demand when the test requests.
/// - StimulateIncomingAsync: append lines to the backing source so an active tail can observe them.
///
/// Notes:
/// - Readiness is coordinated by tests using a sentinel-based handshake; fixtures should not block.
/// - Implementations may be process-backed (file + child process) or fully in-memory.
/// </summary>
public interface ITailMuxFixture
{
    /// <summary>Create and return a mux bound to fixture-managed resources.</summary>
    ITestTailMux CreateMux();

    /// <summary>
    /// Append the provided lines to the mux's backing source (file or in-memory queue), enabling
    /// an active tail to observe them.
    /// </summary>
    Task StimulateIncomingAsync(ITestTailMux mux, IEnumerable<string> lines);

    /// <summary>
    /// Ensure the backing file exists for file-backed muxes. No-op for in-memory implementations.
    /// </summary>
    Task CreateBackingFileAsync(ITestTailMux mux);
}
