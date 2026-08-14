// SPDX-License-Identifier: BUSL-1.1

using Coven.Core.Builder;
using Coven.Core.Covenants;

namespace Coven.Ui.Shell;

/// <summary>
/// CovenServiceBuilder extension methods for the application shell journal.
/// </summary>
public static class UiShellCovenBuilderExtensions
{
    /// <summary>
    /// Adds the shell journal and returns a manifest for declarative covenant configuration.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <returns>A manifest declaring what the shell branch produces and consumes.</returns>
    /// <remarks>
    /// <para>The shell branch:</para>
    /// <list type="bullet">
    /// <item><description>Produces: <see cref="UiNotice"/> — written directly by the application, so the covenant must mark it terminal</description></item>
    /// <item><description>Consumes: <see cref="UiThought"/> — typically routed from <c>AgentThought</c></description></item>
    /// </list>
    /// <para>
    /// The shell has no daemon; the application tails <c>IScrivener&lt;UiEntry&gt;</c> directly.
    /// </para>
    /// </remarks>
    public static BranchManifest UseUiShell(this CovenServiceBuilder coven)
        => UseUiShell(coven, null);

    /// <summary>
    /// Adds the shell journal with optional reasoning support and returns a manifest.
    /// </summary>
    /// <param name="coven">The coven builder.</param>
    /// <param name="configure">Optional callback to configure shell behavior.</param>
    /// <returns>A manifest declaring what the shell branch produces and consumes.</returns>
    public static BranchManifest UseUiShell(
        this CovenServiceBuilder coven,
        Action<UiShellRegistration>? configure)
    {
        ArgumentNullException.ThrowIfNull(coven);

        UiShellRegistration registration = new();
        configure?.Invoke(registration);

        coven.Services.AddUiShell();

        // UiThought is consumed only when the connected agent branch can actually produce
        // reasoning; declaring it unconditionally would demand a route no local model can
        // satisfy.
        HashSet<Type> consumes = [];
        if (registration.ReasoningEnabled)
        {
            consumes.Add(typeof(UiThought));
        }

        return new BranchManifest(
            Name: "UiShell",
            JournalEntryType: typeof(UiEntry),
            Produces: new HashSet<Type> { typeof(UiNotice) },
            Consumes: consumes,
            RequiredDaemons: []);
    }
}
